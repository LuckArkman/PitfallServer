using DTOs;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Services;

public class GameService
{
    private readonly WalletService _walletService;
    readonly WalletWithdrawSnapshot  _walletWithdrawSnapshot;
    private readonly IConfiguration _connectionString;

    public GameService(IConfiguration connectionString,
        WalletWithdrawSnapshot  walletWithdrawSnapshot,
        WalletService  walletService)
    {
        _connectionString = connectionString;
        _walletWithdrawSnapshot = walletWithdrawSnapshot;
        _walletService = walletService;
    }
    
    /// <summary>
    /// Cria uma nova sala de jogo e gera snapshot da carteira do usuário.
    /// </summary>
    public async Task<StartRoundRequest> CreateGameRoom(Guid userId)
    {
        // 🔹 Cria um novo GUID para a partida
        string gameId = Guid.NewGuid().ToString();
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
        return await _walletWithdrawSnapshot.RestoreWallet(gameId);
    }
}