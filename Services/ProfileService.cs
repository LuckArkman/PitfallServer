using Npgsql;
using System.Collections.Generic;
using System.Threading.Tasks;
using Data.Repositories;

namespace Services;

public class ProfileService
{
    private readonly string _connectionString;
    private readonly PostgresUserRepository _authService;
    readonly WalletService  _walletService;

    public ProfileService(string connectionString)
    {
        _connectionString = connectionString;
        _authService = new PostgresUserRepository(connectionString);
        _walletService = new WalletService(connectionString);
    }

    /// <summary>
    /// Retorna o perfil completo do usuário: dados pessoais, carteira e histórico de transações.
    /// </summary>
    public async Task<object?> GetProfile(long userId)
    {
        var user = await _authService.GetByIdAsync(userId);
        object? wallet = await _walletService.GetOrCreateWalletAsync(userId);
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        
        // 🔹 3. Buscar histórico de transações
        var cmdLedger = new NpgsqlCommand(@"
            SELECT 
                ""id"",
                ""UserId"",
                ""Type"",
                ""Amount"",
                ""BalanceAfter"",
                ""GameRoundId"",
                ""Metadata"",
                ""CreatedAt""
            FROM public.wallet_ledger
            WHERE ""UserId"" = @UserId
            ORDER BY ""CreatedAt"" DESC;", conn);
        cmdLedger.Parameters.AddWithValue("@UserId", userId);

        var ledgerList = new List<object>();
        using var ledgerReader = await cmdLedger.ExecuteReaderAsync();
        while (await ledgerReader.ReadAsync())
        {
            ledgerList.Add(new
            {
                id = ledgerReader.GetInt64(0),
                userId = ledgerReader.GetInt64(1),
                Type = ledgerReader.IsDBNull(2) ? "" : ledgerReader.GetString(2),
                Amount = ledgerReader.GetDecimal(3),
                BalanceAfter = ledgerReader.GetDecimal(4),
                GameRoundId = ledgerReader.IsDBNull(5) ? (long?)null : ledgerReader.GetInt64(5),
                Metadata = ledgerReader.IsDBNull(6) ? "{}" : ledgerReader.GetString(6),
                CreatedAt = ledgerReader.GetDateTime(7)
            });
        }

        // 🔹 4. Retornar tudo junto
        return new
        {
            User = user,
            Wallet = wallet,
            Transactions = ledgerList
        };
    }
}
