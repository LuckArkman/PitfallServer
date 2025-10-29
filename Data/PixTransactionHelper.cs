using Npgsql;
using System;
using System.Threading.Tasks;

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
        string qrCode,
        string qrCodeImageUrl)
    {
        const string sql = @"
            INSERT INTO public.pix_transactions (
                user_id, type, id_transaction, amount, status,
                pix_key, pix_key_type, qr_code, qr_code_image_url, created_at
            ) VALUES (
                @user_id, 'PIX_IN', @id_transaction, @amount, 'pending',
                '', '', @qr_code, @qr_code_image_url, NOW()
            )
            RETURNING id;";

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("user_id", userId);
        cmd.Parameters.AddWithValue("id_transaction", idTransaction);
        cmd.Parameters.AddWithValue("amount", amount);
        cmd.Parameters.AddWithValue("qr_code", qrCode ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("qr_code_image_url", qrCodeImageUrl ?? (object)DBNull.Value);

        var result = await cmd.ExecuteScalarAsync();
        return result is long id ? id : throw new Exception("Falha ao obter ID gerado.");
    }
    
    
}