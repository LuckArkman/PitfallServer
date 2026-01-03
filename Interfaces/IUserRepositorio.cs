namespace Interfaces;

public interface IUserRepositorio<T>
{
    Task<T> InsertOneAsync(T document);
    Task<T> GetByIdAsync(Guid id, CancellationToken none);
    void InitializeCollection(string connectionString, string databaseName, string collectionName);
    Task<T?> GetByMailAsync(string email, CancellationToken none);
    Task<T> GetByUserIdAsync(Guid userId, CancellationToken none);
    Task<T?> getUserByCode(string code);
    Task<List<T>?> GetUsersByRegisterCode(string? requesterReferralCode);
}