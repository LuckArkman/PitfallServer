using System;
using System.Threading.Tasks;
using DTOs;
using Npgsql;

namespace Data
{
    /// <summary>
    /// Repositório para operações de persistência da entidade PixTransaction.
    /// Utiliza Npgsql diretamente para acesso ao banco de dados PostgreSQL.
    /// </summary>
    public class PixRepository
    {
        private readonly string _connectionString;

        public PixRepository(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        // --- 🔎 RECUPERAR TRANSAÇÃO POR ID EXTERNO (IdTransaction) ---
        /// <summary>
        /// Recupera uma transação PIX pelo seu ID de transação externo.
        /// As colunas são chamadas pelo nome exato: snake_case (id, user_id, etc.).
        /// </summary>
        public async Task<PixTransaction?> GetByIdTransactionAsync(string idTransaction)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            const string sql = @"
                SELECT 
                    id, user_id, type, id_transaction, amount, status, 
                    pix_key, pix_key_type, qr_code, qr_code_image_url, created_at, paid_at
                FROM public.pix_transactions
                WHERE id_transaction = @IdTransaction
                LIMIT 1;";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@IdTransaction", idTransaction);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync()) return null;

            // Mapeamento manual de colunas (0-indexed) do banco (snake_case) para a DTO (PascalCase)
            return new PixTransaction
            {
                Id = reader.GetInt64(0),
                UserId = reader.GetInt64(1),
                Type = reader.GetString(2),
                IdTransaction = reader.GetString(3),
                Amount = reader.GetDecimal(4),
                Status = reader.GetString(5),
                PixKey = reader.GetString(6),
                PixKeyType = reader.GetString(7),
                QrCode = reader.GetString(8),
                QrCodeImageUrl = reader.GetString(9),
                CreatedAt = reader.GetDateTime(10),
                PaidAt = reader.IsDBNull(11) ? (DateTime?)null : reader.GetDateTime(11)
            };
        }
        
        // --- ➕ CADASTRAR REQUISIÇÃO PIX (INSERT) ---
        /// <summary>
        /// Insere uma nova transação PIX no banco (usado para PIX-IN pendente).
        /// </summary>
        public async Task<long> InsertAsync(PixTransaction transaction)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Usando os nomes exatos das colunas do banco (snake_case)
            const string sql = @"
                INSERT INTO public.pix_transactions (
                    user_id, type, id_transaction, amount, status,
                    pix_key, pix_key_type, qr_code, qr_code_image_url, created_at
                ) VALUES (
                    @UserId, @Type, @IdTransaction, @Amount, @Status,
                    @PixKey, @PixKeyType, @QrCode, @QrCodeImageUrl, NOW()
                )
                RETURNING id;";

            await using var cmd = new NpgsqlCommand(sql, conn);
            // Os parâmetros C# são nomeados com PascalCase para melhor legibilidade
            cmd.Parameters.AddWithValue("@UserId", transaction.UserId);
            cmd.Parameters.AddWithValue("@Type", transaction.Type);
            cmd.Parameters.AddWithValue("@IdTransaction", transaction.IdTransaction);
            cmd.Parameters.AddWithValue("@Amount", transaction.Amount);
            cmd.Parameters.AddWithValue("@Status", transaction.Status);
            cmd.Parameters.AddWithValue("@PixKey", transaction.PixKey ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@PixKeyType", transaction.PixKeyType ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@QrCode", transaction.QrCode ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@QrCodeImageUrl", transaction.QrCodeImageUrl ?? (object)DBNull.Value);

            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt64(result);
        }

        // --- 🔄 ATUALIZAR STATUS DE REQUISIÇÃO PIX (UPDATE) ---
        /// <summary>
        /// Atualiza o status e a data de pagamento de uma transação PIX específica.
        /// </summary>
        public async Task<bool> UpdateStatusAsync(string idTransaction, string newStatus, DateTime? paidAt = null)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Usando os nomes exatos das colunas do banco (snake_case)
            const string sql = @"
                UPDATE public.pix_transactions
                SET 
                    status = @NewStatus,
                    paid_at = @PaidAt
                WHERE 
                    id_transaction = @IdTransaction
                    AND status = 'pending'
                RETURNING id;"; 

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@IdTransaction", idTransaction);
            cmd.Parameters.AddWithValue("@NewStatus", newStatus);
            cmd.Parameters.AddWithValue("@PaidAt", paidAt ?? (object)DBNull.Value);

            var result = await cmd.ExecuteScalarAsync();
            return result != null;
        }
    }
}