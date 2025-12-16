using System;
using System.Threading.Tasks;
using DTOs;
using Npgsql;

namespace Data;

public class AdminRepository
{
    private readonly string _connectionString;

    public AdminRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    // --- 🔹 Cria um novo administrador ---
    public async Task<long> CreateAdminAsync(string email, string passwordHash, string role)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
            INSERT INTO public.""Admins"" (""Email"", ""PasswordHash"", ""Role"", ""CreatedAt"", ""LastLoginAt"")
            VALUES (@Email, @PasswordHash, @Role, NOW(), NULL)
            RETURNING ""Id"";";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Email", email);
        cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
        cmd.Parameters.AddWithValue("@Role", role);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }

    // --- 🔹 Retorna um administrador pelo e-mail ---
    public async Task<Admin?> GetByEmailAsync(string email)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT ""Id"", ""Email"", ""PasswordHash"", ""Role"", ""CreatedAt"", ""LastLoginAt""
            FROM public.""Admins""
            WHERE ""Email"" = @Email;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Email", email);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Admin
            {
                Id = reader.GetInt64(0),
                Email = reader.GetString(1),
                PasswordHash = reader.GetString(2),
                Role = reader.GetString(3),
                CreatedAt = reader.GetDateTime(4),
                LastLoginAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5)
            };
        }

        return null;
    }

    // --- 🔹 Retorna um administrador pelo ID ---
    public async Task<Admin?> GetByIdAsync(long id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT ""Id"", ""Email"", ""PasswordHash"", ""Role"", ""CreatedAt"", ""LastLoginAt""
            FROM public.""Admins""
            WHERE ""Id"" = @Id;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", id);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Admin
            {
                Id = reader.GetInt64(0),
                Email = reader.GetString(1),
                PasswordHash = reader.GetString(2),
                Role = reader.GetString(3),
                CreatedAt = reader.GetDateTime(4),
                LastLoginAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5)
            };
        }

        return null;
    }

    // --- 🔹 Atualiza dados de um administrador ---
    public async Task UpdateAdminAsync(long id, string? passwordHash = null, string? role = null)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"
            UPDATE public.""Admins""
            SET ";

        if (passwordHash != null)
            sql += @"""PasswordHash"" = @PasswordHash, ";

        if (role != null)
            sql += @"""Role"" = @Role, ";

        sql += @"""LastLoginAt"" = ""LastLoginAt""
            WHERE ""Id"" = @Id;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", id);

        if (passwordHash != null)
            cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
        if (role != null)
            cmd.Parameters.AddWithValue("@Role", role);

        await cmd.ExecuteNonQueryAsync();
    }

    // --- 🔹 Atualiza a data do último login ---
    public async Task UpdateLastLoginAsync(long id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
            UPDATE public.""Admins""
            SET ""LastLoginAt"" = NOW()
            WHERE ""Id"" = @Id;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync();
    }
}
