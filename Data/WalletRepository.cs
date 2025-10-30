using DTOs;
using Npgsql;

namespace Data;

public class WalletRepository
{
    private readonly string _connectionString;

    public WalletRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    private NpgsqlConnection GetConnection()
        => new NpgsqlConnection(_connectionString);

    // -------------------------------
    // 🔹 GET WALLET
    // -------------------------------
    public async Task<Wallet?> GetWalletAsync(long userId, NpgsqlConnection? externalConn = null, NpgsqlTransaction? tx = null)
    {
        var ownConn = externalConn == null;
        await using var conn = ownConn ? GetConnection() : null;
        var activeConn = conn ?? externalConn!;
        if (ownConn) await activeConn.OpenAsync();

        const string query = @"
            SELECT ""UserId"", ""Currency"", ""Balance"", ""BalanceWithdrawal"", ""BalanceBonus"", ""UpdatedAt""
            FROM wallets
            WHERE ""UserId"" = @UserId;";

        await using var cmd = new NpgsqlCommand(query, activeConn, tx);
        cmd.Parameters.AddWithValue("@UserId", userId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new Wallet
        {
            UserId = reader.GetInt64(0),
            Currency = reader.GetString(1),
            Balance = reader.GetDecimal(2),
            BalanceWithdrawal = reader.GetDecimal(3),
            BalanceBonus = reader.GetDecimal(4),
            UpdatedAt = reader.GetDateTime(5)
        };
    }

    // -------------------------------
    // 🔹 CREATE WALLET
    // -------------------------------
    public async Task<Wallet> CreateWalletAsync(long userId, NpgsqlConnection conn, NpgsqlTransaction tx)
    {
        const string query = @"
            INSERT INTO wallets (""UserId"", ""Currency"", ""Balance"", ""BalanceWithdrawal"", ""BalanceBonus"", ""UpdatedAt"")
            VALUES (@UserId, 'BRL', 0, 0, 0, NOW())
            RETURNING ""UserId"", ""Currency"", ""Balance"", ""BalanceWithdrawal"", ""BalanceBonus"", ""UpdatedAt"";";

        await using var cmd = new NpgsqlCommand(query, conn, tx);
        cmd.Parameters.AddWithValue("@UserId", userId);

        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();

        return new Wallet
        {
            UserId = reader.GetInt64(0),
            Currency = reader.GetString(1),
            Balance = reader.GetDecimal(2),
            BalanceWithdrawal = reader.GetDecimal(3),
            BalanceBonus = reader.GetDecimal(4),
            UpdatedAt = reader.GetDateTime(5)
        };
    }

    // -------------------------------
    // 🔹 UPDATE WALLET
    // -------------------------------
    public async Task UpdateWalletAsync(Wallet wallet, NpgsqlConnection conn, NpgsqlTransaction tx)
    {
        const string query = @"
            UPDATE wallets
            SET 
                ""Balance"" = @Balance,
                ""BalanceWithdrawal"" = @BalanceWithdrawal,
                ""BalanceBonus"" = @BalanceBonus,
                ""UpdatedAt"" = NOW()
            WHERE ""UserId"" = @UserId;";

        await using var cmd = new NpgsqlCommand(query, conn, tx);
        cmd.Parameters.AddWithValue("@UserId", wallet.UserId);
        cmd.Parameters.AddWithValue("@Balance", wallet.Balance);
        cmd.Parameters.AddWithValue("@BalanceWithdrawal", wallet.BalanceWithdrawal);
        cmd.Parameters.AddWithValue("@BalanceBonus", wallet.BalanceBonus);

        await cmd.ExecuteNonQueryAsync();
    }

    // -------------------------------
    // 🔹 INSERT LEDGER
    // -------------------------------
    public async Task InsertLedgerAsync(long userId, string type, decimal amount, decimal balanceAfter,
                                        NpgsqlConnection conn, NpgsqlTransaction tx,
                                        int round = 0, string metadata = "{}")
    {
        const string query = @"
            INSERT INTO wallet_ledger 
                (""UserId"", ""Type"", ""Amount"", ""BalanceAfter"", ""GameRoundId"", ""Metadata"", ""CreatedAt"")
            VALUES 
                (@UserId, @Type, @Amount, @BalanceAfter, @GameRoundId, @Metadata, NOW());";

        await using var cmd = new NpgsqlCommand(query, conn, tx);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@Type", type);
        cmd.Parameters.AddWithValue("@Amount", amount);
        cmd.Parameters.AddWithValue("@BalanceAfter", balanceAfter);
        cmd.Parameters.AddWithValue("@GameRoundId", round);
        cmd.Parameters.AddWithValue("@Metadata", metadata);

        await cmd.ExecuteNonQueryAsync();
    }

    // -------------------------------
    // 🔹 CREDIT
    // -------------------------------
    public async Task<Wallet> CreditAsync(long userId, decimal amount, string type)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        try
        {
            var wallet = await GetWalletAsync(userId, conn, tx) ?? 
                         await CreateWalletAsync(userId, conn, tx);

            wallet.Balance += amount;
            await UpdateWalletAsync(wallet, conn, tx);
            await InsertLedgerAsync(userId, type, amount, wallet.Balance, conn, tx);

            await tx.CommitAsync();
            return wallet;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // -------------------------------
    // 🔹 DEBIT
    // -------------------------------
    public async Task<Wallet> DebitAsync(long userId, decimal amount, string type)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        try
        {
            var wallet = await GetWalletAsync(userId, conn, tx)
                          ?? throw new InvalidOperationException("Carteira não encontrada.");

            var total = wallet.Balance + wallet.BalanceBonus + wallet.BalanceWithdrawal;
            if (total < amount)
                throw new InvalidOperationException("Saldo insuficiente.");

            var remaining = amount;

            if (wallet.BalanceBonus > 0)
            {
                var debit = Math.Min(wallet.BalanceBonus, remaining);
                wallet.BalanceBonus -= debit;
                remaining -= debit;
            }

            if (remaining > 0 && wallet.Balance > 0)
            {
                var debit = Math.Min(wallet.Balance, remaining);
                wallet.Balance -= debit;
                remaining -= debit;
            }

            if (remaining > 0 && wallet.BalanceWithdrawal > 0)
                wallet.BalanceWithdrawal -= remaining;

            await UpdateWalletAsync(wallet, conn, tx);
            await InsertLedgerAsync(userId, type, -amount, wallet.Balance, conn, tx);

            await tx.CommitAsync();
            return wallet;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
