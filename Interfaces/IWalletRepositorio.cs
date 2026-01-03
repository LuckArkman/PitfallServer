namespace Interfaces;

public interface IWalletRepositorio<T>
{
    void InitializeCollection(string connectionString, string databaseName, string collectionName);
    Task<T?> GetByIdAsync(Guid id, CancellationToken none);
    Task<T?> GetWalletByUserIdAsync(Guid id, CancellationToken none);
    Task<bool> UpdateWallet(T wallet, CancellationToken none);
    Task<T> InsertOneAsync(T document);
}