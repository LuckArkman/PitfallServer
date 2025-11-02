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
        
        // 🔑 StormPag usa Apikey no Header, lida separadamente
        _apiKey = _cfg["StormPag:ApiKey"] ?? throw new ArgumentNullException("StormPag:ApiKey não configurada.");
        
        // Define o cabeçalho de autenticação padrão para o HttpClient
        // O restante das configurações de URL base será feito na chamada
        _http.DefaultRequestHeaders.Clear();
        _http.DefaultRequestHeaders.Add("Apikey", _apiKey);
    }

    public async Task<PixDepositResponse?> CreatePixDepositAsync(PixDepositRequest req, User? user)
    {
        // 🛠️ Mapeamento de campos da FeiPay para StormPag:
        var payload = new 
        {
            nome = req.Name,
            cpf = req.Document,
            valor = req.Amount.ToString("F2"), // StormPag espera string de valor (ex: "100.00")
            descricao = "Depósito via PIX",
            postback = _cfg["StormPag:PostbackUrl"], // URL para receber webhooks
            split = req.SplitPercentage > 0 && !string.IsNullOrWhiteSpace(req.SplitEmail)
                ? new []
                {
                    new 
                    {
                        target = req.SplitEmail, // Assumindo que SplitEmail mapeia para recipient_id/target
                        percentage = req.SplitPercentage
                    }
                }
                : null
        };
        Console.WriteLine(_cfg["StormPag:BaseUrl"] + "/api/v1/cashin");
        var response = await _http.PostAsJsonAsync(_cfg["StormPag:BaseUrl"] + "/api/v1/cashin", payload);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Erro StormPag PIX-IN: {content}");

        // 🔄 Mapeamento da Resposta (Adaptar PixDepositResponse para o formato da StormPag)
        var stormPagResponse = JsonSerializer.Deserialize<StormPagCashInResponse>(content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (stormPagResponse == null) return null;

        // 2. GRAVA NO BANCO IMEDIATAMENTE (ANTES DO WEBHOOK)
        try
        {
            // Mapeando a resposta da StormPag para o objeto local:
            var l = await PixTransactionHelper.InsertPixTransactionManuallyAsync(
                connectionString: _cfg.GetConnectionString("DefaultConnection")!,
                userId: user!.Id,
                idTransaction: stormPagResponse.Id, // StormPag usa "id"
                amount: req.Amount,
                qrCode: stormPagResponse.Pix, // StormPag retorna a string PIX em "pix"
                qrCodeImageUrl: "" // StormPag não retorna URL, apenas string PIX
            );
        }
        catch (Exception ex)
        {
            // Log crítico, mas não interrompe o fluxo
            Console.WriteLine($"[FALHA AO GRAVAR PIX] ID: {stormPagResponse.Id} | Erro: {ex.Message}");
            // Opcional: salvar em fila para retry
        }

        // Retorna a resposta no DTO esperado pelo cliente
        return new PixDepositResponse
        {
            IdTransaction = stormPagResponse.Id,
            QrCode = stormPagResponse.Pix,
            QrCodeImageUrl = "" // Não fornecido pela StormPag
        };
    }

    public async Task<PixWithdrawResponse?> CreatePixWithdrawAsync(PixWithdrawRequest req, User? user)
    {
        // 🛠️ Mapeamento de campos da FeiPay para StormPag:
        // A StormPag não detalha o Cash Out, mas assumimos um formato similar ao Cash In,
        // mas é necessário adaptar `pixKey` e `pixKeyType` para o formato que a StormPag espera
        // para o destinatário (o Cash Out é uma transferência, não uma cobrança).
        // Adaptando para o mínimo necessário do Cash In, assumindo que a StormPag infere a chave PIX do usuário autenticado para Saque.
        var payload = new
        {
            nome = user.Name ?? "Conta Origem",
            cpf = "00000000000",
            valor = req.Amount.ToString("F2"),
            descricao = "Saque de fundos",
            postback = _cfg["StormPag:PostbackUrl"]
        };
        
        // 🚀 Chamada para o endpoint /api/v1/cashout
        var res = await _http.PostAsJsonAsync(_cfg["StormPag:BaseUrl"]?.TrimEnd('/') + "/api/v1/cashout", payload);
        if (!res.IsSuccessStatusCode)
            throw new Exception($"Erro StormPag PIX-OUT: {await res.Content.ReadAsStringAsync()}");

        // Assumindo um DTO de resposta similar ao Cash In para Cash Out
        return await res.Content.ReadFromJsonAsync<PixWithdrawResponse>(); 
    }

    /// <summary>
    /// Cancela automaticamente todas as transações pendentes que expiraram (ex: 15 minutos).
    /// Execute em background (ex: a cada 5 minutos).
    /// </summary>
    /// <param name="expirationMinutes">Tempo máximo para pagamento (padrão: 15 min)</param>
    public async Task CancelExpiredPixTransactionsAsync(int expirationMinutes = 15)
    {
        // Lógica de cancelamento local no DB permanece a mesma.
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
    /// Processa o Webhook da StormPag.
    /// </summary>
    public async Task<bool> ProcessWebhookAsync(PixWebhookDto webhook)
    {
        // O Webhook da StormPag retorna "id", "status" e "value"
        // O DTO deve ser ajustado para mapear `idTransaction` para `id` da StormPag e `amount` para `value`.
        // A lógica interna de idempotência e crédito permanece a mesma.
        if (webhook == null || string.IsNullOrWhiteSpace(webhook.idTransaction))
            throw new ArgumentException("Webhook inválido.");

        // ... [Restante do método ProcessWebhookAsync permanece inalterado]
        // 1. Busca transação
        var pixTx = await _pixRepository.GetByIdTransactionAsync(webhook.idTransaction);

        if (pixTx == null)
        {
            Console.WriteLine($"[WEBHOOK] Transação não encontrada: {webhook.idTransaction}");
            return false;
        }

        // 2. Idempotência
        if (pixTx.Status != "pending")
        {
            Console.WriteLine($"[WEBHOOK] Já processado: {webhook.idTransaction} | Status: Ok");
            return true; // Já foi processado
        }

        // 3. Validação de userId (Pode ser removida se o webhook da StormPag não garantir o userId)
        if (pixTx.UserId != webhook.userId)
        {
            Console.WriteLine($"[WEBHOOK] userId inconsistente! , Webhook: {webhook.userId}");
            return false;
        }

        // 4. Processa status
        bool success = false;

        if (webhook.status == "PAID") // StormPag usa "PAID" em maiúsculas (exemplo)
        {
            pixTx.Status = "Complete";
            pixTx.PaidAt = DateTime.UtcNow;
            await _pixRepository.UpdateStatusAsync(webhook.idTransaction, pixTx.Status, pixTx.PaidAt);

            try
            {
                await _walletService.CreditAsync(pixTx.UserId, webhook.amount, "PIX_IN");
                success = true;
                Console.WriteLine($"[WEBHOOK] Crédito realizado: R$ {webhook.amount} | User: {webhook.userId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WEBHOOK] Falha ao creditar: {ex.Message}");
            }
        }
        else if (webhook.status is "CANCELED" or "EXPIRED")
        {
            pixTx.Status = "Canceled";
            success = true;
        }
        else
        {
            Console.WriteLine($"[WEBHOOK] Status ignorado: {webhook.status}");
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
    /// **REMOVIDO:** A StormPag usa autenticação via header e métodos dedicados, 
    /// tornando este método genérico (`SendToFeiPayAsync`) obsoleto ou desnecessário.
    /// </summary>
    // public async Task<TResponse?> SendToFeiPayAsync<TResponse>(...) {}

    /// <summary>
    /// **REMOVIDO/AJUSTADO:** O monitoramento ativo (`MonitorPixUntilPaidAsync`) 
    /// é um fallback caro e a StormPag não tem endpoint de status documentado. 
    /// A dependência é 100% no Webhook.
    /// </summary>
    // public async Task MonitorPixUntilPaidAsync(string idTransaction, long userId) {}
}

// ⚠️ ADICIONAR ESTE DTO em DTOs/StormPagCashInResponse.cs ou similar

// ⚠️ É necessário garantir que PixDepositResponse e PixWithdrawResponse
// mapeiem corretamente para o que o seu Frontend/Cliente espera.