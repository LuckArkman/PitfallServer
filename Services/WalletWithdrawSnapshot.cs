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
        _ledgerService  = service;
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
            _originalBalance =  wallet.Balance,
            _balanceWithdrawal =  wallet.BalanceWithdrawal,
            _balanceBonus =   wallet.BalanceBonus,
            _createdAt = DateTime.UtcNow
            
        });
        return insert;
    }
}