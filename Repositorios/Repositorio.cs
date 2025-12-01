using Data;
using DTOs;
using Interfaces;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace Repositorios;

public class Repositorio<T> : IRepositorio<T>
{
    private readonly IConfiguration _configuration;
    // Sugestão: Alterar para private readonly ou protected para melhor encapsulamento
    protected IMongoCollection<T> _collection;
    
    // As referências abaixo são privadas ou protegidas, e a Inicialização será feita uma vez.
    private MongoDataController _db { get; set; }
    private IMongoDatabase _mongoDatabase { get; set; }

    public Repositorio(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public void InitializeCollection(string connectionString,
        string databaseName,
        string collectionName)
    {
        // Verifica se a conexão já foi estabelecida
        if (_collection != null) return;
        
        _db = new MongoDataController(connectionString, databaseName, collectionName);
        _mongoDatabase = _db.GetDatabase();

        // O compilador usa o T da CLASSE Repositorio<T>, resolvendo o erro de conversão.
        _collection = _mongoDatabase.GetCollection<T>(collectionName);
    }

    // --- Implementação dos Métodos da Interface (CRUD) ---

    public async Task<T> InsertOneAsync(T document)
    {
        _collection.InsertOne(document);
        return document;
    }
    
    public async Task<T> GetByIdAsync(Guid id, CancellationToken none)
    {
        if (_collection == null) throw new InvalidOperationException("A coleção não foi inicializada.");
        
        // Assume que o ID é mapeado para a propriedade padrão '_id'
        var filter = Builders<T>.Filter.Eq("_id", id);
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<User?> GetByMailAsync(string email, CancellationToken none)
    {
        var collection = _db.GetDatabase().GetCollection<User>("Users");
        var filter = Builders<User>.Filter.Eq(u => u.Email, email);
        return await collection.Find(filter).FirstOrDefaultAsync(none);
    }

    public async Task<Wallet?> UpdateWallet(Wallet wallet, CancellationToken none)
    {
        
        var collection = _db.GetDatabase().GetCollection<Wallet>("Wallets");
        var filter = Builders<Wallet>.Filter.Eq(a => a.Id, wallet.Id);
        var update = Builders<Wallet>.Update
            .Set(a => a.Balance, wallet.Balance)
            .Set(a => a.BalanceBonus, wallet.BalanceBonus)
            .Set(a => a.BalanceWithdrawal, wallet.BalanceWithdrawal)
            .Set(a => a.UpdatedAt, wallet.UpdatedAt);

        var result = await collection.UpdateOneAsync(filter, update, cancellationToken: none);
        return result.ModifiedCount > 0 ? await GetByIdAsync(wallet.Id, none) as Wallet : null;
    }

    public async Task<T> GetByUserIdAsync(Guid userId, CancellationToken none)
    {
        if (_collection == null) throw new InvalidOperationException("A coleção não foi inicializada.");
        var filter = Builders<T>.Filter.Eq("Id", userId);
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<T> GetByIdTransactionAsync(string id)
    {
        if (_collection == null) throw new InvalidOperationException("A coleção não foi inicializada.");
        var filter = Builders<T>.Filter.Eq("idTransaction", id);
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }

    public  async Task<T?> UpdateStatusAsync(string id, string pixTxStatus, DateTime? pixTxPaidAt)
    {
        var collection = _db.GetDatabase().GetCollection<PixTransaction>("Transactions");
        var filter = Builders<PixTransaction>.Filter.Eq(a => a.IdTransaction, id);
        var update = Builders<PixTransaction>.Update
            .Set(a => a.Status, pixTxStatus)
            .Set(a => a.CreatedAt, pixTxPaidAt);

        var result = await collection.UpdateOneAsync(filter, update, cancellationToken: CancellationToken.None);
        return await GetTransactionByIdAsync(id, CancellationToken.None);
    }

    public async Task<List<WalletLedger>?> GetAllWalletLedger(Guid id, CancellationToken none)
    {
        var collection = _mongoDatabase.GetCollection<WalletLedger>("WalletLedger");

        // Cria um filtro para pegar todos os documentos onde o campo "walletId" é igual ao id informado
        var filter = Builders<WalletLedger>.Filter.Eq(x => x.WalletId, id);

        // Busca todos os registros correspondentes
        var result = await collection
            .Find(filter)
            .ToListAsync(none);

        return result;
    }

    public async Task<T?> getUserByCode(string code)
    {
        if (_collection == null) throw new InvalidOperationException("A coleção não foi inicializada.");
        var filter = Builders<T>.Filter.Eq("referralCode", code);
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<UserRanking?> GetUserIdAsync(Guid id, CancellationToken none)
    {
        var collection = _db.GetDatabase().GetCollection<UserRanking>("UserRanking");
        // Assume que o ID é mapeado para a propriedade padrão '_id'
        var filter = Builders<UserRanking>.Filter.Eq(r => r._userId, id);
        return await collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task UpdateRanking(UserRanking? rk, CancellationToken none)
    {
        var collection = _db.GetDatabase().GetCollection<UserRanking>("UserRanking");
        var filter = Builders<UserRanking>.Filter.Eq(a => a.id, rk.id);
        var update = Builders<UserRanking>.Update
            .Set(a => a._level, rk._level)
            .Set(a => a._points, rk._points)
            .Set(a => a._registers, rk._registers)
            .Set(a => a._pointsNextLevel, rk._pointsNextLevel);

        var result = await collection.UpdateOneAsync(filter, update, cancellationToken: CancellationToken.None);
    }

    public async Task<WithdrawSnapshot?> GetRoomIdAsync(string gameId, CancellationToken none)
    {
        var collection = _db.GetDatabase().GetCollection<WithdrawSnapshot>("UserRanking");
        // Assume que o ID é mapeado para a propriedade padrão '_id'
        var filter = Builders<WithdrawSnapshot>.Filter.Eq(r => r._gameRoom,gameId);
        return await collection.Find(filter).FirstOrDefaultAsync();
    }

    private async Task<T> GetTransactionByIdAsync(string id, CancellationToken none)
    {
        if (_collection == null) throw new InvalidOperationException("A coleção não foi inicializada.");
        var filter = Builders<T>.Filter.Eq("idTransaction", id);
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }
}