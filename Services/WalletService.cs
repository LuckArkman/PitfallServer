using Data;
using DTOs;

namespace Services;

public class WalletService
{
    private readonly AppDbContext _db;
    public WalletService(AppDbContext db) { _db = db; }

    public async Task<Wallet> GetOrCreateWalletAsync(long userId)
    {
        var w = await _db.Wallets.FindAsync(userId);
        if (w != null) return w;
        w = new Wallet { UserId = userId, Balance = 0, BalanceWithdrawal = 0, BalanceBonus = 0, UpdatedAt = DateTime.UtcNow };
        _db.Wallets.Add(w);
        await _db.SaveChangesAsync();
        return w;
    }

    public async Task<bool> DebitAsync(long userId, decimal amount, string reason)
    {
        // Simple transactional debit example
        using var tx = await _db.Database.BeginTransactionAsync();
        var wallet = await GetOrCreateWalletAsync(userId);
        if (wallet.Balance + wallet.BalanceBonus + wallet.BalanceWithdrawal < amount) return false;
        wallet.Balance -= amount;
        wallet.UpdatedAt = DateTime.UtcNow;
        _db.WalletLedger.Add(new WalletLedger { UserId = userId, Type = "bet_debit", Amount = -amount, BalanceAfter = wallet.Balance, CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return true;
    }
}