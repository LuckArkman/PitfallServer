using System.Runtime.CompilerServices;
using Data;
using DTOs;
using Microsoft.EntityFrameworkCore;

namespace Services;

public class WalletService
{
    private readonly AppDbContext _db;

    public WalletService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Wallet> GetOrCreateWalletAsync(long userId)
    {
        var w = await _db.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        if (w != null) return w;
        w = new Wallet {BalanceBonus = 0, UserId = userId, Currency = "BRL", Balance = 0, BalanceWithdrawal = 0, UpdatedAt = DateTime.UtcNow };
        _db.Wallets.Add(w);
        await _db.SaveChangesAsync();
        return w;
    }

    /// <summary>
    /// Debita valor da carteira do usuário
    /// </summary>
    public async Task<Wallet> DebitAsync(long userId, decimal amount, string reqType)
    {
        if (amount <= 0)
            throw new ArgumentException("Valor deve ser maior que zero", nameof(amount));

        using var tx = await _db.Database.BeginTransactionAsync();
        
        try
        {
            // ATUALIZAÇÃO: Reutiliza o método GetOrCreateWalletAsync para evitar duplicação de código.
            var wallet = await GetOrCreateWalletAsync(userId);

            // Calcula saldo total disponível
            var totalBalance = wallet.Balance + wallet.BalanceBonus + wallet.BalanceWithdrawal;
            
            if (totalBalance < amount)
                throw new InvalidOperationException("Saldo insuficiente");

            // Debita na ordem: Bonus -> Balance -> Withdrawal
            var remaining = amount;
            
            if (wallet.BalanceBonus > 0)
            {
                var debitBonus = Math.Min(wallet.BalanceBonus, remaining);
                wallet.BalanceBonus -= debitBonus;
                remaining -= debitBonus;
            }
            
            if (remaining > 0 && wallet.Balance > 0)
            {
                var debitBalance = Math.Min(wallet.Balance, remaining);
                wallet.Balance -= debitBalance;
                remaining -= debitBalance;
            }
            
            if (remaining > 0 && wallet.BalanceWithdrawal > 0)
            {
                wallet.BalanceWithdrawal -= remaining;
            }

            wallet.UpdatedAt = DateTime.UtcNow;

            // Registra no ledger
            _db.WalletLedger.Add(new WalletLedger 
            { 
                UserId = userId, 
                Type = reqType,
                Amount = -amount, 
                BalanceAfter = wallet.Balance, 
                Metadata = "{}", // JSON vazio ou null dependendo do schema
                CreatedAt = DateTime.UtcNow 
            });

            await _db.SaveChangesAsync();
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
    /// Credita valor na carteira do usuário
    /// </summary>
    public async Task<Wallet> CreditAsync(long userId, decimal amount, string reqType)
    {
        if (amount <= 0)
            throw new ArgumentException("Valor deve ser maior que zero", nameof(amount));

        using var tx = await _db.Database.BeginTransactionAsync();
        
        try
        {
            // ATUALIZAÇÃO: Reutiliza o método GetOrCreateWalletAsync para evitar duplicação de código.
            var wallet = await GetOrCreateWalletAsync(userId);

            // Adiciona ao balance principal
            wallet.Balance += amount;
            wallet.UpdatedAt = DateTime.UtcNow;

            // Registra no ledger
            _db.WalletLedger.Add(new WalletLedger 
            { 
                UserId = userId, 
                Type = reqType,
                Amount = amount, 
                BalanceAfter = wallet.Balance,
                Metadata = "{}",
                CreatedAt = DateTime.UtcNow 
            });

            await _db.SaveChangesAsync();
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