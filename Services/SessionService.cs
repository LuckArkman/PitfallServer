using Data;
using DTOs;
using Microsoft.EntityFrameworkCore;

namespace Services;

public class SessionService
{
    private readonly SessionDbContext _sessionDb; // Conexão com o SQLite
    private readonly AppDbContext _mainDb;      // Conexão com o PostgreSQL
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromHours(8);

    public SessionService(SessionDbContext sessionDb, AppDbContext mainDb)
    {
        _sessionDb = sessionDb;
        _mainDb = mainDb;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        // Garante que o objeto passado seja um usuário para extrair o ID
        if (value is not User user)
        {
            throw new InvalidOperationException("SessionService só pode armazenar objetos do tipo User.");
        }

        var expiresAt = DateTime.UtcNow.Add(expiration ?? DefaultExpiration);
        var existingSession = await _sessionDb.UserSessions.FindAsync(key);

        if (existingSession != null)
        {
            // Atualiza a sessão existente
            existingSession.UserId = user.Id;
            existingSession.ExpiresAtUtc = expiresAt;
        }
        else
        {
            // Cria uma nova sessão com o UserId
            var newSession = new UserSession
            {
                SessionToken = key,
                UserId = user.Id,
                ExpiresAtUtc = expiresAt
            };
            _sessionDb.UserSessions.Add(newSession);
        }

        await _sessionDb.SaveChangesAsync();
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        // Garante que a requisição seja para um objeto User
        if (typeof(T) != typeof(User))
        {
            return default;
        }

        // 1. Busca a sessão no banco de dados SQLite
        var session = await _sessionDb.UserSessions.AsNoTracking().FirstOrDefaultAsync(s => s.SessionToken == key);

        if (session == null)
            return default;

        // 2. Verifica se a sessão expirou
        if (session.ExpiresAtUtc < DateTime.UtcNow)
        {
            _sessionDb.UserSessions.Remove(session);
            await _sessionDb.SaveChangesAsync();
            return default;
        }

        // 3. Usa o UserId da sessão para buscar o usuário completo no PostgreSQL
        var user = await _mainDb.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == session.UserId);
        
        return (T?)(object?)user;
    }

    public async Task RemoveAsync(string key)
    {
        var session = await _sessionDb.UserSessions.FindAsync(key);
        if (session != null)
        {
            _sessionDb.UserSessions.Remove(session);
            await _sessionDb.SaveChangesAsync();
        }
    }
}