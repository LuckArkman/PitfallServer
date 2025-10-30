using Npgsql;
using System;
using System.Threading.Tasks;
using NpgsqlTypes;

namespace Data;

public static class PixTransactionHelper
{
    /// <summary>
    /// Insere uma transação PIX manualmente no banco PostgreSQL.
    /// Usa Npgsql diretamente (sem EF Core).
    /// </summary>
    /// <param name="connectionString">String de conexão do PostgreSQL</param>
    /// <param name="userId">ID do usuário</param>
    /// <param name="idTransaction">ID retornado pela FeiPay</param>
    /// <param name="amount">Valor do depósito</param>
    /// <param name="qrCode">Código EMV (BR Code)</param>
    /// <param name="qrCodeImageUrl">URL da imagem do QR Code</param>
    /// <returns>ID gerado no banco</returns>
    public static async Task<long> InsertPixTransactionManuallyAsync(
        string connectionString,
        long userId,
        string idTransaction,
        decimal amount,
        string? qrCode,
        string? qrCodeImageUrl,
        CancellationToken ct = default)
    {
        const string sql = @"
        INSERT INTO public.pix_transactions (
            ""user_id"", ""type"", ""id_transaction"", ""amount"", ""status"",
            ""pix_key"", ""pix_key_type"", ""qr_code"", ""qr_code_image_url"", ""created_at""
        ) VALUES (
            @user_id, 'PIX_IN', @id_transaction, @amount, 'pending',
            '', '', @qr_code, @qr_code_image_url, NOW()
        )
        RETURNING id;";

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("user_id", userId);
        cmd.Parameters.AddWithValue("id_transaction", idTransaction);
        cmd.Parameters.AddWithValue("amount", amount);
        cmd.Parameters.AddWithValue("qr_code", (object?)qrCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("qr_code_image_url", (object?)qrCodeImageUrl ?? DBNull.Value);

        var scalar = await cmd.ExecuteScalarAsync(ct);

        // Converte com segurança independente do provider retornar int64, int32 etc.
        if (scalar is null || scalar is DBNull)
            throw new InvalidOperationException("Falha ao obter ID gerado.");

        // Tenta paths mais comuns antes do Convert
        if (scalar is long l) return l;
        if (scalar is int i) return i;

        return Convert.ToInt64(scalar, System.Globalization.CultureInfo.InvariantCulture);
    }    
}