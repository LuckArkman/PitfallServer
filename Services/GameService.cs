using DTOs;
using Npgsql;

namespace Services;

public class GameService
{
    private readonly WalletService _walletService;
    private readonly string _connectionString;

    public GameService(string connectionString)
    {
        _connectionString = connectionString;
        _walletService = new WalletService(connectionString);
    }
    
    /// <summary>
    /// Cria uma nova sala de jogo e gera snapshot da carteira do usuário.
    /// </summary>
    public async Task<StartRoundRequest> CreateGameRoom(long userId)
    {
        // 🔹 Cria um novo GUID para a partida
        string gameId = Guid.NewGuid().ToString();
        // 🔹 Cria o snapshot da carteira do usuário (com GameId)
        await _walletService.CreateWithdrawSnapshotAsync(userId, gameId);

        // 🔹 Retorna o DTO para o cliente
        var response = new StartRoundRequest
        {
            UserId = userId,
            GameId = gameId
        };

        Console.WriteLine($"[GameRoom] Snapshot criado para usuário {userId}, partida {gameId}");
        return response;
    }
    
    /// <summary>
    /// Restaura a conta do usuário para o estado original do snapshot.
    /// </summary>
    public async Task<object?> RestoreSnapshotAccount(string gameId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        // 🔹 Busca snapshot e dados da carteira original
        var cmd = new NpgsqlCommand(@"
            SELECT s.""UserId"", s.""OriginalWithdraw"", w.""Balance""
            FROM public.wallets_snapshot s
            JOIN public.wallets w ON w.""UserId"" = s.""UserId""
            WHERE s.""GameId"" = @GameId
            LIMIT 1;", conn);

        cmd.Parameters.AddWithValue("@GameId", gameId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (!reader.Read())
            return null;

        long userId = reader.GetInt64(0);
        decimal originalWithdraw = reader.GetDecimal(1);
        decimal currentBalance = reader.GetDecimal(2);

        reader.Close();

        // 🔹 Retorna informações para o cliente
        return new
        {
            UserId = userId,
            GameId = gameId,
            Balance = currentBalance,
            OriginalWithdraw = originalWithdraw
        };
    }
}