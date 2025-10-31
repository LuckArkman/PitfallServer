using Npgsql;
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

        /// <summary>
        /// Cadastra um novo usuário no banco
        /// </summary>
        public async Task<long> RegisterAsync(string email, string name, string passwordHash)
        {
            const string sql = @"
                INSERT INTO public.users 
                    (""Id"", ""email"", ""name"", ""PasswordHash"", ""is_influencer"", ""status"", ""CreatedAt"", ""UpdatedAt"")
                VALUES 
                    (nextval('users_id_seq'), @email, @name, @PasswordHash, FALSE, 'active', NOW(), NOW())
                RETURNING ""Id"";";

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("email", email);
            command.Parameters.AddWithValue("name", name);
            command.Parameters.AddWithValue("PasswordHash", passwordHash);

            var id = await command.ExecuteScalarAsync();
            return Convert.ToInt64(id);
        }

        /// <summary>
        /// Recupera usuário por email
        /// </summary>
        public async Task<User?> GetByEmailAsync(string email)
        {
            const string sql = @"
                SELECT ""Id"", ""email"", ""name"", ""PasswordHash"", ""is_influencer"", ""status"", ""CreatedAt"", ""UpdatedAt""
                FROM public.users
                WHERE ""email"" = @email
                LIMIT 1;";

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("email", email);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new User
                {
                    Id = reader.GetInt64(0),
                    Email = reader.GetString(1),
                    Name = reader.GetString(2),
                    PasswordHash = reader.GetString(3),
                    IsInfluencer = reader.GetBoolean(4),
                    Status = reader.GetString(5),
                    CreatedAt = reader.GetDateTime(6),
                    UpdatedAt = reader.GetDateTime(7)
                };
            }

            return null;
        }

        /// <summary>
        /// Verifica se o email já existe
        /// </summary>
        public async Task<bool> EmailExistsAsync(string email)
        {
            const string sql = @"SELECT 1 FROM public.users WHERE ""email"" = @email LIMIT 1;";

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("email", email);

            return await command.ExecuteScalarAsync() != null;
        }

        public async Task<User?> GetByIdAsync(long userId)
        {
            const string sql = @"
                SELECT ""Id"", ""email"", ""name"", ""PasswordHash"", ""is_influencer"", ""status"", ""CreatedAt"", ""UpdatedAt""
                FROM public.users
                WHERE ""Id"" = @id
                LIMIT 1;";

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id", userId);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new User
                {
                    Id = reader.GetInt64(0),
                    Email = reader.GetString(1),
                    Name = reader.GetString(2),
                    PasswordHash = reader.GetString(3),
                    IsInfluencer = reader.GetBoolean(4),
                    Status = reader.GetString(5),
                    CreatedAt = reader.GetDateTime(6),
                    UpdatedAt = reader.GetDateTime(7)
                };
            }

            return null;
        }
    }
}