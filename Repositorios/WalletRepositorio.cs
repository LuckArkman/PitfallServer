using Data;
using DTOs;
using Interfaces;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace Repositorios;

public class WalletRepositorio : IWalletRepositorio<Wallet>
{
    private readonly IConfiguration _configuration;
    
    protected IMongoCollection<Wallet> _collection;
    private MongoDataController _db { get; set; }
    private IMongoDatabase _mongoDatabase { get; set; }

    public void InitializeCollection(string connectionString,
        string databaseName,
        string collectionName)
    {
        if (_collection != null) return;
        
        _db = new MongoDataController(connectionString, databaseName, collectionName);
        _mongoDatabase = _db.GetDatabase();
        _collection = _mongoDatabase.GetCollection<Wallet>(collectionName);
    }

    public async Task<Wallet?> GetByIdAsync(Guid id, CancellationToken none)
    {
        if (_collection == null) throw new InvalidOperationException("A coleção não foi inicializada.");
        var filter = Builders<Wallet>.Filter.Eq(w => w.Id, id);
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<Wallet?> GetWalletByUserIdAsync(Guid id, CancellationToken none)
    {
        if (_collection == null) throw new InvalidOperationException("A coleção não foi inicializada.");
        var filter = Builders<Wallet>.Filter.Eq(w => w.UserId, id);
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<bool> UpdateWallet(Wallet wallet, CancellationToken none)
    {
        var collection = _db.GetDatabase().GetCollection<Wallet>("Wallets");
        var filter = Builders<Wallet>.Filter.Eq(a => a.Id, wallet.Id);
        var update = Builders<Wallet>.Update
            .Set(a => a.Balance, wallet.Balance)
            .Set(a => a.BalanceBonus, wallet.BalanceBonus)
            .Set(a => a.BalanceWithdrawal, wallet.BalanceWithdrawal)
            .Set(a => a.UpdatedAt, wallet.UpdatedAt);

        var result = await collection.UpdateOneAsync(filter, update, cancellationToken: none);
        return true;
    }

    public async Task<Wallet?> InsertOneAsync(Wallet document)
    {
        await _collection.InsertOneAsync(document);
        return document;
    }
}