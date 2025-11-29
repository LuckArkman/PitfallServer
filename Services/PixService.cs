using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Data;
using DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Npgsql;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Services;

public class PixService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;
    private readonly WalletService _walletService;
    private PixRepository _pixRepository;
    private readonly string _apiKey; // Nova variável para a API Key

    public PixService(HttpClient http, IConfiguration cfg,
        WalletService walletService)
    {
        _http = http;
        _cfg = cfg;
        _walletService = walletService;
        _pixRepository = new PixRepository(_cfg["ConnectionStrings:DefaultConnection"]);
        _apiKey = _cfg["agilizepay:CLIENT_ID"];
        _http.DefaultRequestHeaders.Clear();
        _http.DefaultRequestHeaders.Add("client_id", _apiKey);
    }

    public async Task<PixCharge?> CreatePixDepositAsync(PixDepositRequest req, User? user)
    {
        var payload = new PaymentRequest
        {
            token = _cfg["agilizepay:Token"],
            secret = _cfg["agilizepay:CLIENT_SECRET"],
            postback = _cfg["agilizepay:PostbackUrl"],
            amount = req.Amount,
            debtor_name = user.Name,
            email = user.Email,
            debtor_document_number = req.Document,
            phone = "(00) 00000 - 0000",
            method_pay = "pix",
            split_email = user.Email,
            split_percentage = 0,
        };
        Console.WriteLine($"{_cfg["agilizepay:BaseUrl"]
    } >> {JsonConvert.SerializeObject(payload)}");

    var response = await _http.PostAsJsonAsync(_cfg["agilizepay:BaseUrl"], payload);
        var content = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"{nameof(CreatePixDepositAsync)} >> {content}");

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Erro AgilizePay PIX-IN: {content}");
        var stormPagResponse = JsonSerializer.Deserialize<PixResponse>(content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (stormPagResponse == null) return null;
        try
        {
            var l = await PixTransactionHelper.InsertPixTransactionManuallyAsync(
                connectionString: _cfg.GetConnectionString("DefaultConnection")!,
                userId: user!.Id,
                idTransaction: stormPagResponse.txid, 
                amount: req.Amount,
                qrCode: stormPagResponse.qrCode,
                cpf: req.Document,
                qrCodeImageUrl: ""
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FALHA AO GRAVAR PIX] ID: {stormPagResponse.txid} | Erro: {ex.Message}");
        }
        return new PixCharge
        {
            Id = stormPagResponse.txid,
            QrCodeImageUrl = stormPagResponse.pixCopiaECola,
            BrCode = stormPagResponse.qrCode
        };
    }

    public async Task<PixWithdrawResponse?> CreatePixWithdrawAsync(PixWithdrawRequest req, User? user)
    {
        
        var payload = new
        {
            nome = user.Name ?? "Conta Origem",
            cpf = "00000000000",
            valor = req.Amount.ToString("F2"),
            descricao = "Saque de fundos",
            postback = _cfg["agilizepay:PostbackUrl"]
        };
        var res = await _http.PostAsJsonAsync(_cfg["agilizepay:BaseUrl"]?.TrimEnd('/') + "/api/v1/cashout", payload);
        if (!res.IsSuccessStatusCode)
            throw new Exception($"Erro agilizepay PIX-OUT: {await res.Content.ReadAsStringAsync()}");
        
        return await res.Content.ReadFromJsonAsync<PixWithdrawResponse>(); 
    }
    
    public async Task CancelExpiredPixTransactionsAsync(int expirationMinutes = 600)
    {
        var connectionString = _cfg.GetConnectionString("DefaultConnection");
        var cutoff = DateTime.UtcNow.AddMinutes(-expirationMinutes);

        const string sql = @"
        UPDATE public.pix_transactions
        SET 
            status = 'Canceled',
            paid_at = NULL
        WHERE 
            status = 'pending'
            AND created_at < @cutoff;";

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("cutoff", cutoff);

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        if (rowsAffected > 0)
            Console.WriteLine($"[PIX] {rowsAffected} transações expiradas foram canceladas.");
    }

    /// <summary>
    /// Processa o Webhook da agilizepay.
    /// </summary>
    public async Task<bool> ProcessWebhookAsync(Transaction webhook)
    {
        if (webhook == null || string.IsNullOrWhiteSpace(webhook.IdTransaction))
            throw new ArgumentException("Webhook inválido.");

        // ... [Restante do método ProcessWebhookAsync permanece inalterado]
        // 1. Busca transação
        var pixTx = await _pixRepository.GetByIdTransactionAsync(webhook.IdTransaction);

        if (pixTx == null)
        {
            Console.WriteLine($"[WEBHOOK] Transação não encontrada: {webhook.IdTransaction}");
            return false;
        }

        // 2. Idempotência
        if (pixTx.Status != "pending")
        {
            Console.WriteLine($"[WEBHOOK] Já processado: {webhook.IdTransaction} | Status: Ok");
            return true; // Já foi processado
        }
        // 4. Processa status
        bool success = false;

        if (webhook.TypeTransaction == "PAID_IN") // agilizepay usa "PAID" em maiúsculas (exemplo)
        {
            pixTx.Status = "Complete";
            pixTx.PaidAt = DateTime.UtcNow;
            await _pixRepository.UpdateStatusAsync(webhook.IdTransaction, pixTx.Status, pixTx.PaidAt);

            try
            {
                await _walletService.CreditAsync(pixTx.UserId, pixTx.Amount, "PIX_IN");
                success = true;
                Console.WriteLine($"[WEBHOOK] Crédito realizado: R$ {pixTx.Amount} | User: {pixTx.UserId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WEBHOOK] Falha ao creditar: {ex.Message}");
            }
        }
        else if (webhook.StatusTransaction  != "sucesso")
        {
            pixTx.Status = "Canceled";
            success = true;
        }
        else
        {
            Console.WriteLine($"[WEBHOOK] Status ignorado: {webhook.StatusTransaction}");
            return true;
        }

        return success;
    }

    /// <summary>
    /// Atualiza o status da transação PIX com base no webhook ou expiração.
    /// </summary>
    public async Task<bool> UpdatePixTransactionStatusAsync(
        string idTransaction,
        string newStatus, // "paid" ou "canceled"
        DateTime? paidAt = null)
    {
        // [Método de atualização de status local no DB, permanece o mesmo]
        if (string.IsNullOrWhiteSpace(idTransaction))
            throw new ArgumentException("idTransaction é obrigatório.");

        var validStatuses = new[] { "paid", "canceled" };
        if (!validStatuses.Contains(newStatus))
            throw new ArgumentException("Status inválido. Use 'paid' ou 'canceled'.");

        var connectionString = _cfg.GetConnectionString("DefaultConnection");
        const string sql = @"
            UPDATE public.pix_transactions
            SET 
                status = @status,
                paid_at = @paid_at
            WHERE 
                id_transaction = @id_transaction
                AND status = 'pending'
            RETURNING id;";

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id_transaction", idTransaction);
        cmd.Parameters.AddWithValue("status", newStatus == "paid" ? "Complete" : "Canceled");
        cmd.Parameters.AddWithValue("paid_at", paidAt ?? (object)DBNull.Value);

        var result = await cmd.ExecuteScalarAsync();
        return result != null;
    }

    /// <summary>
    /// **REMOVIDO:** A agilizepay usa autenticação via header e métodos dedicados, 
    /// tornando este método genérico (`SendToFeiPayAsync`) obsoleto ou desnecessário.
    /// </summary>
    // public async Task<TResponse?> SendToFeiPayAsync<TResponse>(...) {}

    /// <summary>
    /// **REMOVIDO/AJUSTADO:** O monitoramento ativo (`MonitorPixUntilPaidAsync`) 
    /// é um fallback caro e a agilizepay não tem endpoint de status documentado. 
    /// A dependência é 100% no Webhook.
    /// </summary>
    // public async Task MonitorPixUntilPaidAsync(string idTransaction, long userId) {}
}

// ⚠️ ADICIONAR ESTE DTO em DTOs/agilizepayCashInResponse.cs ou similar

// ⚠️ É necessário garantir que PixDepositResponse e PixWithdrawResponse
// mapeiem corretamente para o que o seu Frontend/Cliente espera.