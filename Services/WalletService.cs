using DTOs;
using Interfaces;
using Microsoft.Extensions.Configuration;

namespace Services;

public class WalletService
{
    private readonly IWalletRepositorio<Wallet> _repositorio;
    private readonly IUserRepositorio<User> _repositorioUser;
    private readonly WalletLedgerService _ledgerService;
    private readonly WalletWithdrawSnapshot _walletWithdrawSnapshot;
    private readonly IConfiguration _cfg;

    public WalletService(IConfiguration connectionString,
        WalletWithdrawSnapshot  walletWithdrawSnapshot,
        IUserRepositorio<User> repositorioUser,
        IWalletRepositorio<Wallet> repositorio,
        WalletLedgerService service)
    {
        _ledgerService  = service;
        _walletWithdrawSnapshot = walletWithdrawSnapshot;
        _repositorioUser = repositorioUser;
        _repositorio = repositorio;
        _cfg = connectionString;
        _repositorio.InitializeCollection(_cfg["MongoDbSettings:ConnectionString"],
            _cfg["MongoDbSettings:DataBaseName"],
            "Wallets");
        _repositorioUser.InitializeCollection(_cfg["MongoDbSettings:ConnectionString"],
            _cfg["MongoDbSettings:DataBaseName"],
            "Users");
    }

    /// <summary>
    /// Retorna a carteira de um usuário ou cria uma nova se não existir.
    /// </summary>
    public async Task<Wallet?> GetOrCreateWalletAsync(
        Guid userId)
    {
        
        Wallet? wallet = await _repositorio.GetWalletByUserIdAsync(
            id: userId,
            none: CancellationToken.None);
        if (wallet == null){
            wallet = await _repositorio.InsertOneAsync(
                         new Wallet
                         {
                             UserId = userId,
                             Balance = 0.0m,
                             BalanceWithdrawal = 0.0m,
                             BalanceBonus = 0.0m,
                             UpdatedAt = DateTime.UtcNow
                         });
            
        }
        
        return wallet;
    }

    public async Task<Wallet> CreditAsync(Guid userId, decimal amount, string type)
    {
        if (amount <= 0)
            throw new InvalidOperationException("O valor deve ser maior que zero.");
        var _user = await _repositorioUser.GetByIdAsync(userId, CancellationToken.None);
        var wallet = await _repositorio.GetWalletByUserIdAsync(
            id: userId,
            none: CancellationToken.None);

        var user = await _repositorio.GetWalletByUserIdAsync(userId, CancellationToken.None);
        
        decimal withdrawalPart = amount * 0.8m;
        decimal mainBalancePart = amount * 0.2m;
        decimal newBalance = wallet.Balance + mainBalancePart;
        decimal newBalanceWithdrawal = wallet.BalanceWithdrawal + withdrawalPart;
        decimal balanceAfter = wallet.Balance + newBalanceWithdrawal + wallet.BalanceBonus;
        if (wallet != null)
        {
            wallet.Balance = newBalance;
            wallet.BalanceWithdrawal = newBalanceWithdrawal;
            wallet.UpdatedAt = DateTime.UtcNow;
            

            var update = await _repositorio.UpdateWallet(wallet, CancellationToken.None);
        }
        
        WalletLedger ledger = new WalletLedger
        {
            WalletId = wallet.Id,
            IsInfluencer = _user.IsInfluencer,
            Type = type ?? "credit",
            Amount = amount,
            BalanceBefore = wallet.Balance,
            BalanceAfter = balanceAfter,
            GameRoundId = 0,
            Metadata = $"{{\"split\":\"80/20\",\"withdraw\":{withdrawalPart},\"main\":{mainBalancePart}}}",
            CreatedAt = DateTime.UtcNow
        };
        var insert = await _ledgerService.CreditService(ledger);

        return wallet;
    }

    /// <summary>
    /// Debita um valor da carteira do usuário.
    /// </summary>
    public async Task<Wallet> DebitAsync(Guid userId, decimal amount, string reqType)
    {
        if (amount <= 0)
            throw new ArgumentException("O valor deve ser maior que zero.", nameof(amount));

        var wallet = await GetOrCreateWalletAsync(
            userId: userId);
        Console.WriteLine($"{nameof(DebitAsync)} >> {wallet == null}");
        decimal withdrawalPart = amount * 0.8m;
        decimal mainBalancePart = amount * 0.2m;
        decimal newBalance = wallet.Balance - amount;
        decimal newBalanceWithdrawal = wallet.BalanceWithdrawal + withdrawalPart;
        decimal balanceAfter = wallet.Balance + newBalanceWithdrawal + wallet.BalanceBonus;
        if (wallet != null)
        {
            wallet.UpdatedAt = DateTime.UtcNow;
            wallet.Balance = newBalance;

            var update = await _repositorio.UpdateWallet(wallet, CancellationToken.None);
        }

        WalletLedger ledger = new WalletLedger
        {
            WalletId = wallet.Id,
            Type = reqType ?? "dedit",
            Amount = amount,
            BalanceAfter = balanceAfter,
            GameRoundId = 0,
            Metadata = $"{{\"split\":\"80/20\",\"withdraw\":{withdrawalPart},\"main\":{mainBalancePart}}}",
            CreatedAt = DateTime.UtcNow
        };
        
        await _ledgerService.CreditService(ledger);
        
        return wallet;
    }

    public async Task<Wallet?> CreateWithdrawSnapshotAsync(Guid userId, string gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
            throw new ArgumentException("O ID da partida é obrigatório.", nameof(gameId));
        
        var wallet = await GetOrCreateWalletAsync(
            userId: userId) as Wallet;
        var snap = await _walletWithdrawSnapshot.CreateSnap(wallet, gameId);
        Console.WriteLine(
                    $"[Snapshot] Usuário {userId} → partida {gameId} → withdraw original salvo: {wallet.BalanceWithdrawal}");
        
        return wallet;
    }
}