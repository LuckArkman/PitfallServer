using Microsoft.Data.Sqlite;
using DTOs;

namespace Services
{
    public class SessionService
    {
        private readonly string _connectionString;
        private static readonly TimeSpan DefaultExpiration = TimeSpan.FromHours(8);

        public SessionService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task SetAsync(string key, User user, TimeSpan? expiration = null)
        {
            var expiresAt = DateTime.UtcNow.Add(expiration ?? DefaultExpiration);

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Cria tabela se não existir
            var createTableCmd = connection.CreateCommand();
            createTableCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS user_sessions (
                    SessionToken TEXT PRIMARY KEY,
                    UserId INTEGER NOT NULL,
                    ExpiresAtUtc TEXT NOT NULL
                );";
            await createTableCmd.ExecuteNonQueryAsync();

            // Verifica se já existe sessão
            var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT COUNT(*) FROM user_sessions WHERE SessionToken = @key;";
            selectCmd.Parameters.AddWithValue("@key", key);

            var exists = Convert.ToInt32(await selectCmd.ExecuteScalarAsync()) > 0;

            if (exists)
            {
                // Atualiza sessão existente
                var updateCmd = connection.CreateCommand();
                updateCmd.CommandText = @"
                    UPDATE user_sessions
                    SET UserId = @userId, ExpiresAtUtc = @expiresAt
                    WHERE SessionToken = @key;";
                updateCmd.Parameters.AddWithValue("@userId", user.Id);
                updateCmd.Parameters.AddWithValue("@expiresAt", expiresAt.ToString("o"));
                updateCmd.Parameters.AddWithValue("@key", key);
                await updateCmd.ExecuteNonQueryAsync();
            }
            else
            {
                // Cria nova sessão
                var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = @"
                    INSERT INTO user_sessions (SessionToken, UserId, ExpiresAtUtc)
                    VALUES (@key, @userId, @expiresAt);";
                insertCmd.Parameters.AddWithValue("@key", key);
                insertCmd.Parameters.AddWithValue("@userId", user.Id);
                insertCmd.Parameters.AddWithValue("@expiresAt", expiresAt.ToString("o"));
                await insertCmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<UserSession?> GetAsync(string key)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = @"
        SELECT SessionToken, UserId, ExpiresAtUtc
        FROM user_sessions
        WHERE SessionToken = @key;";
            selectCmd.Parameters.AddWithValue("@key", key);

            using var reader = await selectCmd.ExecuteReaderAsync();

            if (!reader.Read())
                return null;

            var session = new UserSession
            {
                SessionToken = reader.GetString(0),
                UserId = reader.GetInt64(1),
                ExpiresAtUtc = DateTime.Parse(reader.GetString(2))
            };

            // Verifica se expirou
            if (session == null)
            {
                await RemoveAsync(key);
                return null;
            }

            return session;
        }


        public async Task RemoveAsync(string key)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var deleteCmd = connection.CreateCommand();
            deleteCmd.CommandText = "DELETE FROM user_sessions WHERE SessionToken = @key;";
            deleteCmd.Parameters.AddWithValue("@key", key);
            await deleteCmd.ExecuteNonQueryAsync();
        }
    }
}
