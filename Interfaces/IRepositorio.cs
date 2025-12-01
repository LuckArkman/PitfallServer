using DTOs;

namespace Interfaces;

public interface IRepositorio<T> 
{
    Task<T> InsertOneAsync(T document);
    Task<T> GetByIdAsync(Guid id, CancellationToken none);
    void InitializeCollection(string connectionString, string databaseName, string collectionName);
    Task<T> GetByMailAsync(string email, CancellationToken none);
    Task<Wallet> UpdateWallet(Wallet wallet, CancellationToken none);
    Task<T> GetByUserIdAsync(Guid userId, CancellationToken none);
    Task<T> GetByIdTransactionAsync(string id);
    Task<T?> UpdateStatusAsync(string id, string pixTxStatus, DateTime? pixTxPaidAt);
    Task<List<WalletLedger>?> GetAllWalletLedger(Guid id, CancellationToken none);
    Task<T?> getUserByCode(string code);
    Task<UserRanking?> GetUserIdAsync(Guid id, CancellationToken none);
    Task UpdateRanking(UserRanking? rk, CancellationToken none);
}