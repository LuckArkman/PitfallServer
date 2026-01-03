using DTOs;

namespace Interfaces;

public interface IWalletLedgerRepositorio<T>
{
    void InitializeCollection(string connectionString, string databaseName, string collectionName);
    Task<T> InsertOneAsync(T document);
    Task<List<WalletLedger>?> GetAllWalletLedger(Guid userId, CancellationToken none);
}