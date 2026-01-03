using Data;
using DTOs;
using Interfaces;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Repositorios;

public class UserRepositorio : IUserRepositorio<User>
{
    private readonly IConfiguration _configuration;
    // Sugestão: Alterar para private readonly ou protected para melhor encapsulamento
    protected IMongoCollection<User> _collection;
    
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
        _collection = _mongoDatabase.GetCollection<User>(collectionName);
    }
    public async Task<User> InsertOneAsync(User document)
    {
        await _collection.InsertOneAsync(document);
        return document;
    }

    public async Task<User> GetByIdAsync(Guid id, CancellationToken none)
    {
        var filter = Builders<User>.Filter.Eq(u => u.Id, id);
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<User?> GetByMailAsync(string email, CancellationToken none)
    {
        var filter = Builders<User>.Filter.Eq(u => u.Email, email);
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<User> GetByUserIdAsync(Guid userId, CancellationToken none)
    {
        var filter = Builders<User>.Filter.Eq(u => u.Id, userId);
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<User?> getUserByCode(string code)
    {
        var filter = Builders<User>.Filter.Eq(u => u.ReferralCode, code);
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<List<User>?> GetUsersByRegisterCode(string? registerCode)
    {
        if (string.IsNullOrWhiteSpace(registerCode))
            return new List<User>();
        
        var filter = Builders<User>.Filter.Eq(u => u.registerCode, registerCode);
    
        var users = await _collection
            .Find(filter)
            .ToListAsync();
    
        return users ?? new List<User>();
    }
}