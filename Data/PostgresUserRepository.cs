using Npgsql;
using System;
using System.Threading.Tasks;
using DTOs;

namespace Data.Repositories
{
    public class PostgresUserRepository
    {
        private readonly string _connectionString;

        public PostgresUserRepository(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        // ============================================================
        //  REGISTRO COM REFERRAL (Método Modificado)
        // ============================================================
        public async Task<long> RegisterAsync(
            string email, 
            string name, 
            string passwordHash,
            Guid? inviterL1, // <--- Novos Parâmetros
            Guid? inviterL2,
            Guid? inviterL3
        )
        {
            // O comando INSERT deve incluir as novas colunas
            const string sql = @"
                INSERT INTO public.users 
                    (""email"", ""name"", ""password_hash"", ""is_influencer"", ""status"", ""created_at"", ""updated_at"", 
                     inviter_l1_id, inviter_l2_id, inviter_l3_id)
                VALUES 
                    (@email, @name, @password_hash, FALSE, 'active', NOW(), NOW(),
                     @l1_id, @l2_id, @l3_id)
                RETURNING ""id"";
            ";

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("email", email);
            command.Parameters.AddWithValue("name", name);
            command.Parameters.AddWithValue("password_hash", passwordHash);
            
            // Persistência dos IDs (UUID/Guid)
            command.Parameters.AddWithValue("l1_id", inviterL1 ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("l2_id", inviterL2 ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("l3_id", inviterL3 ?? (object)DBNull.Value);

            var id = await command.ExecuteScalarAsync();
            return id != null ? (long)id : 0;
        }

        // ============================================================
        //  GET BY EMAIL (Método Modificado)
        // ============================================================
        public async Task<User?> GetByEmailAsync(string email)
        {
            // Selecionar as novas colunas
            const string sql = @"
                SELECT ""id"", ""email"", ""name"", ""password_hash"", ""is_influencer"", ""status"", ""created_at"", ""updated_at"",
                       inviter_l1_id, inviter_l2_id, inviter_l3_id 
                FROM public.users WHERE ""email"" = @email LIMIT 1;
            ";

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("email", email);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                // A lógica de leitura abaixo (índices 8, 9, 10) já estava correta 
                // se a query SELECT for atualizada.
                return new User
                {
                    Id = reader.GetInt64(0),
                    Email = reader.GetString(1),
                    Name = reader.GetString(2),
                    PasswordHash = reader.GetString(3),
                    IsInfluencer = reader.GetBoolean(4),
                    Status = reader.GetString(5),
                    CreatedAt = reader.GetDateTime(6),
                    UpdatedAt = reader.GetDateTime(7),
                    InviterL1 = reader.IsDBNull(8) ? null : reader.GetGuid(8),
                    InviterL2 = reader.IsDBNull(9) ? null : reader.GetGuid(9),
                    InviterL3 = reader.IsDBNull(10) ? null : reader.GetGuid(10)
                };
            }

            return null;
        }

        // ============================================================
        //  GET BY ID (Método Modificado)
        // ============================================================
        public async Task<User?> GetByIdAsync(long userId)
        {
            // Selecionar as novas colunas
            const string sql = @"
                SELECT ""id"", ""email"", ""name"", ""password_hash"", ""is_influencer"", ""status"", ""created_at"", ""updated_at"",
                       inviter_l1_id, inviter_l2_id, inviter_l3_id 
                FROM public.users WHERE ""id"" = @id LIMIT 1;
            ";

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id", userId);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                // A lógica de leitura já estava correta se a query SELECT for atualizada.
                return new User
                {
                    Id = reader.GetInt64(0),
                    Email = reader.GetString(1),
                    Name = reader.GetString(2),
                    PasswordHash = reader.GetString(3),
                    IsInfluencer = reader.GetBoolean(4),
                    Status = reader.GetString(5),
                    CreatedAt = reader.GetDateTime(6),
                    UpdatedAt = reader.GetDateTime(7),
                    InviterL1 = reader.IsDBNull(8) ? null : reader.GetGuid(8),
                    InviterL2 = reader.IsDBNull(9) ? null : reader.GetGuid(9),
                    InviterL3 = reader.IsDBNull(10) ? null : reader.GetGuid(10)
                };
            }

            return null;
        }
        
        public async Task<bool> EmailExistsAsync(string email)
        {
            const string sql = @"SELECT 1 FROM public.users WHERE ""email"" = @email LIMIT 1;";

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("email", email);

            return await command.ExecuteScalarAsync() != null;
        }
    }
}