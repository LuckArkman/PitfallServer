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
            RETURNING id::bigint;";

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);

        cmd.Parameters.Add(new NpgsqlParameter<long>("user_id", NpgsqlDbType.Bigint) { Value = userId });
        cmd.Parameters.Add(new NpgsqlParameter<string>("id_transaction", NpgsqlDbType.Varchar) { Value = idTransaction });
        cmd.Parameters.Add(new NpgsqlParameter<decimal>("amount", NpgsqlDbType.Numeric) { Value = amount });
        cmd.Parameters.Add(new NpgsqlParameter<string?>("qr_code", NpgsqlDbType.Text) { Value = (object?)qrCode ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter<string?>("qr_code_image_url", NpgsqlDbType.Text) { Value = (object?)qrCodeImageUrl ?? DBNull.Value });

        // (Opcional) melhora desempenho em chamadas repetidas
        await cmd.PrepareAsync(ct);

        var id = await cmd.ExecuteScalarAsync<long>(ct);
        return id;
    }
    
    
}