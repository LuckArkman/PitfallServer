namespace Interfaces;

public interface IRequestTransactionRepositorio<T>
{
    void InitializeCollection(string connectionString,
        string databaseName,
        string collectionName);
    Task<T> InsertOneAsync(T document);
    Task<T?> GetByIdTransactionAsync(string id);
    Task<T?> UpdateStatusAsync(string id, string pixTxStatus, DateTime? pixTxPaidAt);
}