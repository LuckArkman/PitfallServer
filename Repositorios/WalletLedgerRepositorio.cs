using Data;
using DTOs;
using Interfaces;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace Repositorios;

public class WalletLedgerRepositorio : IWalletLedgerRepositorio<WalletLedger>
{
    private readonly IConfiguration _configuration;
    protected IMongoCollection<WalletLedger> _collection;
    private MongoDataController _db { get; set; }
    private IMongoDatabase _mongoDatabase { get; set; }
    
    public void InitializeCollection(string connectionString,
        string databaseName,
        string collectionName)
    {
        if (_collection != null) return;
        _db = new MongoDataController(connectionString, databaseName, collectionName);
        _mongoDatabase = _db.GetDatabase();
        _collection = _mongoDatabase.GetCollection<WalletLedger>(collectionName);
    }

    public async Task<WalletLedger> InsertOneAsync(WalletLedger document)
    {
        await _collection.InsertOneAsync(document);
        return document;
    }

    public async Task<List<WalletLedger>?> GetAllWalletLedger(Guid id, CancellationToken none)
    {
        var filter = Builders<WalletLedger>.Filter.Eq(x => x.WalletId, id);
        var result = await _collection
            .Find(filter)
            .ToListAsync(none);

        return result;
    }
}