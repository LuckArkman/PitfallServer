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
    /// <summary>
    /// Insere (ou atualiza se já existir) uma transação PIX-IN e retorna o ID (bigint).
    /// Requer UNIQUE em (id_transaction).
    /// </summary>
    public static async Task<long> InsertPixTransactionManuallyAsync(
        string connectionString,
        long userId,
        string idTransaction,
        decimal amount,
        string? qrCode,
        string? cpf,
        string? qrCodeImageUrl,
        string? pixKey = null,
        string? pixKeyType = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("connectionString inválida.", nameof(connectionString));
        if (string.IsNullOrWhiteSpace(idTransaction))
            throw new ArgumentException("idTransaction é obrigatório.", nameof(idTransaction));
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "amount deve ser > 0");

            const string sql = @"
    INSERT INTO public.pix_transactions (
        ""user_id"", ""type"", ""id_transaction"", ""amount"", ""status"",
        ""pix_key"", ""pix_key_type"", ""qr_code"",""cpf"", ""qr_code_image_url"", ""created_at""
    ) VALUES (
        @user_id, 'PIX_IN', @id_transaction, @amount, 'pending',
        @pix_key, @pix_key_type, @qr_code,@cpf, @qr_code_image_url, CURRENT_TIMESTAMP AT TIME ZONE 'UTC'
    )
    ON CONFLICT (""id_transaction"")
    DO UPDATE SET
        ""amount"" = EXCLUDED.""amount"",
        ""qr_code"" = EXCLUDED.""qr_code"",
        ""cpf"" = EXCLUDED.""cpf"",
        ""qr_code_image_url"" = EXCLUDED.""qr_code_image_url""
    RETURNING id::bigint;";

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);

        cmd.Parameters.Add(new NpgsqlParameter<long>("user_id", NpgsqlDbType.Bigint) { Value = userId });
        cmd.Parameters.Add(new NpgsqlParameter<string>("id_transaction", NpgsqlDbType.Text) { Value = idTransaction });
        cmd.Parameters.Add(new NpgsqlParameter<decimal>("amount", NpgsqlDbType.Numeric) { Value = amount });

        // Campos NOT NULL na sua tabela: garantimos string vazia quando vier null
        cmd.Parameters.Add(new NpgsqlParameter<string>("pix_key", NpgsqlDbType.Text) { Value = pixKey ?? string.Empty });
        cmd.Parameters.Add(new NpgsqlParameter<string>("pix_key_type", NpgsqlDbType.Text) { Value = pixKeyType ?? string.Empty });
        cmd.Parameters.Add(new NpgsqlParameter<string>("qr_code", NpgsqlDbType.Text) { Value = qrCode ?? string.Empty });
        cmd.Parameters.Add(new NpgsqlParameter<string>("cpf", NpgsqlDbType.Text) { Value = cpf ?? string.Empty });
        cmd.Parameters.Add(new NpgsqlParameter<string>("qr_code_image_url", NpgsqlDbType.Text) { Value = qrCodeImageUrl ?? string.Empty });

        await cmd.PrepareAsync(ct);

        var scalar = await cmd.ExecuteScalarAsync(ct);
        // retorno sempre bigint por causa do cast no SQL
        if (scalar is long l) return l;
        if (scalar is int i) return i;
        return Convert.ToInt64(scalar);
    }
}