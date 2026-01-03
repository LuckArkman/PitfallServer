using Data;
using DTOs;
using Interfaces;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace Repositorios;

public class RequestTransactionRepositorio : IRequestTransactionRepositorio<RequestTransaction>
{
    private readonly IConfiguration _configuration;
    // Sugestão: Alterar para private readonly ou protected para melhor encapsulamento
    protected IMongoCollection<RequestTransaction> _collection;
    
    // As referências abaixo são privadas ou protegidas, e a Inicialização será feita uma vez.
    private MongoDataController _db { get; set; }
    private IMongoDatabase _mongoDatabase { get; set; }
    
    public void InitializeCollection(string connectionString,
        string databaseName,
        string collectionName)
    {
        // Verifica se a conexão já foi estabelecida
        if (_collection != null) return;
        
        _db = new MongoDataController(connectionString, databaseName, collectionName);
        _mongoDatabase = _db.GetDatabase();

        // O compilador usa o T da CLASSE Repositorio<T>, resolvendo o erro de conversão.
        _collection = _mongoDatabase.GetCollection<RequestTransaction>(collectionName);
    }

    public async Task<RequestTransaction> InsertOneAsync(RequestTransaction document)
    {
        await _collection.InsertOneAsync(document);
        return document;
    }

    public async Task<RequestTransaction?> GetByIdTransactionAsync(string id)
    {
        var filter = Builders<RequestTransaction>.Filter.Eq(p => p.id, id);
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<RequestTransaction?> UpdateStatusAsync(string id, string pixTxStatus, DateTime? pixTxPaidAt)
    {
        var filter = Builders<RequestTransaction>.Filter.Eq(p => p.id, id);
        var update = Builders<RequestTransaction>.Update
            .Set(a => a.Status, pixTxStatus)
            .Set(a => a.PaidAt, pixTxPaidAt);
        
        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: CancellationToken.None);
        return await GetByIdTransactionAsync(id);
    }
}