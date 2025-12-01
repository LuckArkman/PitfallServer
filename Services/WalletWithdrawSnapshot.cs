using DTOs;
using Interfaces;
using Microsoft.Extensions.Configuration;

namespace Services;

public class WalletWithdrawSnapshot
{
    private readonly IRepositorio<WithdrawSnapshot> _repositorio;
    private readonly WalletLedgerService _ledgerService;
    private readonly IConfiguration _cfg;

    public WalletWithdrawSnapshot(IConfiguration connectionString,
        IRepositorio<WithdrawSnapshot> repositorio,
        WalletLedgerService service)
    {
        _ledgerService = service;
        _repositorio = repositorio;
        _cfg = connectionString;
        _repositorio.InitializeCollection(_cfg["MongoDbSettings:ConnectionString"],
            _cfg["MongoDbSettings:DataBaseName"],
            "WithdrawSnapshots");
    }

    public async Task<WithdrawSnapshot?> CreateSnap(Wallet wallet, string gameRoom)
    {
        var insert = await _repositorio.InsertOneAsync(new WithdrawSnapshot
        {
            _walletId = wallet.Id,
            _gameRoom = gameRoom,
            _originalBalance = wallet.Balance,
            _balanceWithdrawal = wallet.BalanceWithdrawal,
            _balanceBonus = wallet.BalanceBonus,
            _createdAt = DateTime.UtcNow
        });
        return insert;
    }

    public async Task<Wallet?> RestoreWallet(string gameId)
    {
        var insert = await _repositorio.GetRoomIdAsync(gameId, CancellationToken.None);
        if (insert != null)
        {
            var w = new Wallet
            {
                Id = insert._walletId,
                UserId = Guid.NewGuid(),
                Balance = insert._originalBalance,
                BalanceBonus = insert._balanceBonus,
                BalanceWithdrawal = insert._balanceWithdrawal,
                UpdatedAt = DateTime.UtcNow
            };
            var wallet = await _repositorio.UpdateWallet(w, CancellationToken.None);
            return wallet;
        }

        return null;
    }
}