using Data;
using DTOs;
using Npgsql;
using System;
using System.Threading.Tasks;

namespace Services;

public class ReferralService
{
    private readonly AppDbContext _db;
    private readonly string _baseUrl;

    public ReferralService(AppDbContext db, string baseUrl)
    {
        _db = db;
        // Define um fallback para a URL base se não estiver configurada
        _baseUrl = baseUrl?.TrimEnd('/') ?? "https://meusite.com";
    }

    // =============================================================
    // Gera o link de convite
    // =============================================================
    public async Task<string> GenerateReferralLinkAsync(User user)
    {
        if (user == null) throw new ArgumentNullException(nameof(user));

        if (string.IsNullOrWhiteSpace(user.ReferralCode))
        {
            string code = await GenerateUniqueReferralCode();

            // CORREÇÃO: Uso de 'referral_code' na query de UPDATE
            string sql = @$"
                UPDATE public.users SET referral_code = @code
                WHERE id = @uid
            ";

            await _db.ExecuteNonQueryAsync(sql,
                new NpgsqlParameter("@code", code),
                new NpgsqlParameter("@uid", user.Id));

            user.ReferralCode = code;
        }

        return $"{_baseUrl}/registro?ref={Uri.EscapeDataString(user.ReferralCode)}";
    }

    // =============================================================
    // Gera um código único
    // =============================================================
    private async Task<string> GenerateUniqueReferralCode()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            string candidate = Guid.NewGuid().ToString("N")[..10];

            // CORREÇÃO: Uso de 'referral_code' na query de SELECT COUNT
            string checkSql = "SELECT COUNT(*) FROM public.users WHERE referral_code = @code"; 

            long count = await _db.ExecuteScalarAsync<long>(
                checkSql, new NpgsqlParameter("@code", candidate));

            if (count == 0)
                return candidate;
        }

        // fallback
        return Guid.NewGuid().ToString("N")[..12];
    }

    // =============================================================
    // Retorna cadeia de uplines (L1, L2, L3)
    // L1 = O usuário encontrado pelo código (inviter do novo usuário)
    // L2 = O inviter do L1
    // L3 = O inviter do L2
    // =============================================================
    public async Task<(Guid? inviterL1, Guid? inviterL2, Guid? inviterL3)>
        AttachReferralChainFromRefAsync(string refCode)
    {
        if (string.IsNullOrWhiteSpace(refCode))
            return (null, null, null);

        // CORREÇÃO: Uso de 'referral_code' na query de SELECT
        string sql = @$"
            SELECT id, inviter_l1_id, inviter_l2_id
            FROM public.users
            WHERE referral_code = @code
            LIMIT 1
        ";

        using var reader = await _db.ExecuteQueryAsync(
            sql,
            new NpgsqlParameter("@code", refCode)
        );

        if (!await reader.ReadAsync())
            return (null, null, null);

        // L1 (id): É BIGINT (long) no banco, mas deve ser Guid? no retorno.
        // É feito um "hack" para converter o long para um Guid.
        long l1IdLong = reader.GetInt64(0);
        string hexString = l1IdLong.ToString("X").PadLeft(32, '0');
        Guid? inviterL1 = new Guid(hexString);
        
        // L2 (inviter_l1_id): Guid? no banco.
        Guid? inviterL2 = reader.IsDBNull(1) ? null : reader.GetGuid(1);
        
        // L3 (inviter_l2_id): Guid? no banco.
        Guid? inviterL3 = reader.IsDBNull(2) ? null : reader.GetGuid(2);
        
        return (inviterL1, inviterL2, inviterL3);
    }
}