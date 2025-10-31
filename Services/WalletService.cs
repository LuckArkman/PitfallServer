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

    /// <summary>
    /// Credita um valor na carteira do usuário.
    /// </summary>
    public async Task<Wallet> CreditAsync(long userId, decimal amount, string reqType)
    {
        if (amount <= 0)
            throw new ArgumentException("O valor deve ser maior que zero.", nameof(amount));

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        try
        {
            var wallet = await GetOrCreateWalletAsync(userId, conn, tx);

            wallet.Balance += amount;
            wallet.UpdatedAt = DateTime.UtcNow;

            await _walletRepository.UpdateWalletAsync(wallet, conn, tx);
            await _walletRepository.InsertLedgerAsync(userId, reqType, amount, wallet.Balance, conn, tx);

            await tx.CommitAsync();
            return wallet;
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
}
