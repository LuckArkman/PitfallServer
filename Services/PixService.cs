using System.Net.Http.Json;
using System.Text.Json;
using Data;
using DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Services;

public class PixService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;
    private readonly WalletService _walletService;
    private PixRepository _pixRepository;

    public PixService(HttpClient http, IConfiguration cfg,
        WalletService walletService)
    {
        _http = http;
        _cfg = cfg;
        _walletService = walletService;
        _pixRepository = new PixRepository(_cfg["ConnectionStrings:DefaultConnection"]);
    }

    public async Task<PixDepositResponse?> CreatePixDepositAsync(PixDepositRequest req, User? user)
    {
        var payload = new
        {
            token = _cfg["FeiPay:Token"],
            secret = _cfg["FeiPay:Secret"],
            postback = _cfg["FeiPay:PostbackUrl"],
            amount = req.Amount,
            debtor_name = req.Name,
            email = req.Email,
            debtor_document_number = req.Document,
            phone = req.Phone,
            method_pay = "pix",
            split_email = req.SplitEmail ?? "",
            split_percentage = req.SplitPercentage > 0 ? req.SplitPercentage.ToString() : ""
        };

        var response = await _http.PostAsJsonAsync("wallet/deposit/payment", payload);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Erro FeiPay PIX-IN: {content}");

        var obj = JsonSerializer.Deserialize<PixDepositResponse>(content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (obj == null) return null;

        // 2. GRAVA NO BANCO IMEDIATAMENTE (ANTES DO WEBHOOK)
        try
        {
            var l = await PixTransactionHelper.InsertPixTransactionManuallyAsync(
                connectionString: _cfg["ConnectionStrings:DefaultConnection"],
                user.Id,
                obj.IdTransaction,
                req.Amount,
                obj.QrCode,
                obj.QrCodeImageUrl
            );
        }
        catch (Exception ex)
        {
            // Log crítico, mas não interrompe o fluxo
            Console.WriteLine($"[FALHA AO GRAVAR PIX] ID: {obj.IdTransaction} | Erro: {ex.Message}");
            // Opcional: salvar em fila para retry
        }

        return obj;
    }

    public async Task<PixWithdrawResponse?> CreatePixWithdrawAsync(PixWithdrawRequest req)
    {
        var payload = new
        {
            token = _cfg["FeiPay:Token"],
            secret = _cfg["FeiPay:Secret"],
            baasPostbackUrl = _cfg["FeiPay:PostbackUrl"],
            amount = req.Amount,
            pixKey = req.PixKey,
            pixKeyType = req.PixKeyType
        };

        var res = await _http.PostAsJsonAsync("pixout", payload);
        if (!res.IsSuccessStatusCode)
            throw new Exception($"Erro FeiPay PIX-OUT: {await res.Content.ReadAsStringAsync()}");

        return await res.Content.ReadFromJsonAsync<PixWithdrawResponse>();
    }
    /// <summary>
    /// Cancela automaticamente todas as transações pendentes que expiraram (ex: 15 minutos).
    /// Execute em background (ex: a cada 5 minutos).
    /// </summary>
    /// <param name="expirationMinutes">Tempo máximo para pagamento (padrão: 15 min)</param>
    public async Task CancelExpiredPixTransactionsAsync(int expirationMinutes = 15)
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
    
// Services/PixService.cs
    public async Task<bool> ProcessWebhookAsync(PixWebhookDto webhook)
    {
        if (webhook == null || string.IsNullOrWhiteSpace(webhook.idTransaction))
            throw new ArgumentException("Webhook inválido.");

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

        // 3. Validação de userId
        if (pixTx.UserId != webhook.userId)
        {
            Console.WriteLine($"[WEBHOOK] userId inconsistente! , Webhook: {webhook.userId}");
            return false;
        }

        // 4. Processa status
        bool success = false;

        if (webhook.status == "paid")
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
        else if (webhook.status is "canceled" or "expired")
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
    /// <param name="idTransaction">ID retornado pela FeiPay</param>
    /// <param name="newStatus">"paid" ou "canceled"</param>
    /// <param name="paidAt">Data/hora do pagamento (opcional)</param>
    /// <returns>true se atualizado, false se não encontrado ou já finalizado</returns>
    public async Task<bool> UpdatePixTransactionStatusAsync(
        string idTransaction,
        string newStatus, // "paid" ou "canceled"
        DateTime? paidAt = null)
    {
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
}