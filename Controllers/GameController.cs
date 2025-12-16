using DTOs;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace Controllers;

[ApiController]
[Route("api/[controller]")]
public class GameController : ControllerBase
{
    private readonly GameService _gameService;
    readonly SessionService _session;

    public GameController(GameService pixService,
        GameService session,
        SessionService sessionService)
    {
        _gameService = session;
        _session = sessionService;
    }

    // ================================= PIX IN =================================
    [HttpPost("GameRoom")]
    public async Task<IActionResult> GameRoom([FromBody] Room dto)
    {
        Console.WriteLine($"{nameof(GameRoom)} >> {dto == null}");
        var session = await _session.GetAsync(dto.token);
        if (session == null) return BadRequest(new { message = "Sessão inválida" });
        var room = await _gameService.CreateGameRoom(session.UserId);
        return Ok(room);
    }
    
    // 🔹 Restaura snapshot da conta (usando GameId)
    [HttpGet("Restore/{gameId}")]
    public async Task<IActionResult> RestoreSnapshot(string gameId)
    {
        var snapshot = await _gameService.RestoreSnapshotAccount(gameId);
        if (snapshot == null)
            return NotFound(new { message = "Snapshot não encontrado para este ID de partida." });

        return Ok(snapshot);
    }
}