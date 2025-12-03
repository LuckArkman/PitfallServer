using DTOs;

namespace Interfaces;

public interface IRepositorio<T> 
{
    Task<T> InsertOneAsync(T document);
    Task<T> GetByIdAsync(Guid id, CancellationToken none);
    void InitializeCollection(string connectionString, string databaseName, string collectionName);
    Task<User?> GetByMailAsync(string email, CancellationToken none);
    Task<bool> UpdateWallet(Wallet wallet, CancellationToken none);
    Task<T> GetByUserIdAsync(Guid userId, CancellationToken none);
    Task<T> GetByIdTransactionAsync(string id);
    Task<T?> UpdateStatusAsync(string id, string pixTxStatus, DateTime? pixTxPaidAt);
    Task<List<WalletLedger>?> GetAllWalletLedger(Guid id, CancellationToken none);
    Task<T?> getUserByCode(string code);
    Task<UserRanking?> GetUserIdAsync(Guid id, CancellationToken none);
    Task UpdateRanking(UserRanking? rk, CancellationToken none);
    Task<WithdrawSnapshot?> GetRoomIdAsync(string gameId, CancellationToken none);
    Task<T?> GetWalletByUserIdAsync(Guid userId, CancellationToken none);
}