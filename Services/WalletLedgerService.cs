using DTOs;
using Interfaces;
using Microsoft.Extensions.Configuration;

namespace Services;

public class WalletLedgerService
{
    private readonly IRepositorio<WalletLedger> _repositorio;
    private readonly IConfiguration _cfg;
    public WalletLedgerService(IConfiguration connectionString,
        IRepositorio<WalletLedger> repositorio)
    {
        _repositorio = repositorio;
        _cfg = connectionString;
        _repositorio.InitializeCollection(_cfg["MongoDbSettings:ConnectionString"],
            _cfg["MongoDbSettings:DataBaseName"],
            "WalletLedger");
    }

    public async Task<WalletLedger> CreditService(WalletLedger  ledger)
    {
        var wallet = await _repositorio.InsertOneAsync(ledger);
        return wallet;
    }

    public async Task<List<WalletLedger?>> GetAllLedger(Guid userId)
    {
       var all = await _repositorio.GetAllWalletLedger(userId, CancellationToken.None);
       return all;
    }
}