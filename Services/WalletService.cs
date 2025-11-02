using Data;
using DTOs;
using Npgsql;

namespace Services;

public class WalletService
{
    private readonly WalletRepository _walletRepository;
    private readonly string _connectionString;

    public WalletService(string connectionString)
    {
        _connectionString = connectionString;
        _walletRepository = new WalletRepository(connectionString);
    }

    /// <summary>
    /// Retorna a carteira de um usuário ou cria uma nova se não existir.
    /// </summary>
    public async Task<Wallet> GetOrCreateWalletAsync(
        long userId,
        NpgsqlConnection? conn = null,
        NpgsqlTransaction? tx = null)
    {
        if (conn == null)
        {
            await using var localConn = new NpgsqlConnection(_connectionString);
            await localConn.OpenAsync();
            await using var localTx = await localConn.BeginTransactionAsync();

            var wallet = await _walletRepository.GetWalletAsync(userId, localConn, localTx)
                         ?? await _walletRepository.CreateWalletAsync(userId, localConn, localTx);

            await localTx.CommitAsync();
            return wallet;
        }

        var existing = await _walletRepository.GetWalletAsync(userId, conn, tx);
        return existing ?? await _walletRepository.CreateWalletAsync(userId, conn, tx);
    }

    public async Task<Wallet> CreditAsync(long userId, decimal amount, string type)
    {
        if (amount <= 0)
            throw new InvalidOperationException("O valor deve ser maior que zero.");

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var tx = await conn.BeginTransactionAsync();

        try
        {
            var cmdSelect = new NpgsqlCommand(@"
            SELECT ""Balance"", ""BalanceWithdrawal"", ""BalanceBonus"", ""Currency""
            FROM public.wallets
            WHERE ""UserId"" = @p_userId
            FOR UPDATE", conn, tx);

            cmdSelect.Parameters.AddWithValue("@p_userId", userId);

            using var reader = await cmdSelect.ExecuteReaderAsync();
            if (!reader.Read())
                throw new InvalidOperationException("Carteira não encontrada.");

            decimal balance = reader.GetDecimal(0);
            decimal balanceWithdrawal = reader.GetDecimal(1);
            decimal balanceBonus = reader.GetDecimal(2);
            string currency = reader.GetString(3);
            reader.Close();

            decimal withdrawalPart = amount * 0.8m;
            decimal mainBalancePart = amount * 0.2m;

            var cmdUpdate = new NpgsqlCommand(@"
            UPDATE public.wallets
            SET 
                ""Balance"" = ""Balance"" + @p_main,
                ""BalanceWithdrawal"" = ""BalanceWithdrawal"" + @p_withdraw,
                ""UpdatedAt"" = NOW()
            WHERE ""UserId"" = @p_userId", conn, tx);

            cmdUpdate.Parameters.AddWithValue("@p_userId", userId);
            cmdUpdate.Parameters.AddWithValue("@p_main", mainBalancePart);
            cmdUpdate.Parameters.AddWithValue("@p_withdraw", withdrawalPart);

            await cmdUpdate.ExecuteNonQueryAsync();

            decimal newBalance = balance + mainBalancePart;
            decimal newBalanceWithdrawal = balanceWithdrawal + withdrawalPart;

            var cmdLedger = new NpgsqlCommand(@"
            INSERT INTO public.wallet_ledger 
            (""UserId"", ""Type"", ""Amount"", ""BalanceAfter"", ""Metadata"", ""CreatedAt"")
            VALUES (@p_userId, @p_type, @p_amount, @p_balanceAfter, @p_metadata, NOW())", conn, tx);

            decimal balanceAfter = newBalance + newBalanceWithdrawal + balanceBonus;

            cmdLedger.Parameters.AddWithValue("@p_userId", userId);
            cmdLedger.Parameters.AddWithValue("@p_type", type ?? "credit");
            cmdLedger.Parameters.AddWithValue("@p_amount", amount);
            cmdLedger.Parameters.AddWithValue("@p_balanceAfter", balanceAfter);
            cmdLedger.Parameters.AddWithValue("@p_metadata",
                $"{{\"split\":\"80/20\",\"withdraw\":{withdrawalPart},\"main\":{mainBalancePart}}}");

            await cmdLedger.ExecuteNonQueryAsync();

            await tx.CommitAsync();

            return new Wallet
            {
                UserId = userId,
                Balance = newBalance,
                BalanceWithdrawal = newBalanceWithdrawal,
                BalanceBonus = balanceBonus,
                Currency = currency,
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Debita um valor da carteira do usuário.
    /// </summary>
    public async Task<Wallet> DebitAsync(long userId, decimal amount, string reqType)
    {
        if (amount <= 0)
            throw new ArgumentException("O valor deve ser maior que zero.", nameof(amount));

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        try
        {
            var wallet = await GetOrCreateWalletAsync(userId, conn, tx);

            var totalBalance = wallet.Balance + wallet.BalanceBonus + wallet.BalanceWithdrawal;
            if (totalBalance < amount)
                throw new InvalidOperationException("Saldo insuficiente.");

            var remaining = amount;

            // 🔹 Usa primeiro o bônus
            if (wallet.BalanceBonus > 0)
            {
                var debit = Math.Min(wallet.BalanceBonus, remaining);
                wallet.BalanceBonus -= debit;
                remaining -= debit;
            }

            // 🔹 Depois saldo principal
            if (remaining > 0 && wallet.Balance > 0)
            {
                var debit = Math.Min(wallet.Balance, remaining);
                wallet.Balance -= debit;
                remaining -= debit;
            }

            // 🔹 Finalmente, saldo de saque (withdrawal)
            if (remaining > 0 && wallet.BalanceWithdrawal > 0)
                wallet.BalanceWithdrawal -= remaining;

            wallet.UpdatedAt = DateTime.UtcNow;

            await _walletRepository.UpdateWalletAsync(wallet, conn, tx);
            await _walletRepository.InsertLedgerAsync(userId, reqType, -amount, wallet.Balance, conn, tx);

            await tx.CommitAsync();
            return wallet;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task CreateWithdrawSnapshotAsync(long userId, string gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
            throw new ArgumentException("O ID da partida é obrigatório.", nameof(gameId));

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var wallet = await GetOrCreateWalletAsync(userId, conn);

        // 🔹 Salva snapshot do withdraw original
        var cmd = new NpgsqlCommand(@"
        INSERT INTO public.wallets_snapshot 
        (""UserId"", ""GameId"",""OriginalBalance"", ""OriginalWithdraw"", ""CreatedAt"")
        VALUES (@UserId, @GameId, @OriginalBalance, @OriginalWithdraw, NOW())
        ON CONFLICT (""UserId"", ""GameId"")
        DO UPDATE SET 
            ""OriginalWithdraw"" = EXCLUDED.""OriginalWithdraw"",
            ""CreatedAt"" = NOW();", conn);

        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@GameId", gameId);
        cmd.Parameters.AddWithValue("@OriginalBalance", wallet.Balance);
        cmd.Parameters.AddWithValue("@OriginalWithdraw", wallet.BalanceWithdrawal);
        cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

        await cmd.ExecuteNonQueryAsync();

        Console.WriteLine(
            $"[Snapshot] Usuário {userId} → partida {gameId} → withdraw original salvo: {wallet.BalanceWithdrawal}");
    }
}