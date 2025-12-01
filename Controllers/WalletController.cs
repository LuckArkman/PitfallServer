using DTOs;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace Controllers;

[ApiController]
[Route("api/[controller]")]
public class WalletController : ControllerBase
{
    readonly WalletService _service;
    private readonly AuthService _authService;
    readonly SessionService _sessionService;
    private readonly GameService _gameService;

    public WalletController(WalletService service,
        SessionService sessionService,
        GameService  gameService,
        AuthService authService)
    {
        _service = service;
        _sessionService = sessionService;
        _gameService = gameService;
        _authService = authService;
    }

    [HttpPost("wallet")]
    public async Task<IActionResult> GetWallet([FromBody] RequestWallet req)
    {
        if (string.IsNullOrWhiteSpace(req.token))
            return BadRequest(new { message = "Token não fornecido." });

        var user = await _sessionService.GetAsync(req.token) as UserSession;
        if (user == null)
            return Ok(new { message = "Sessão inválida ou expirada." });

        var wallet = await _service.GetOrCreateWalletAsync(user.UserId);

        return Ok(new
        {
            balance = wallet.Balance,
            balanceBonus = wallet.BalanceBonus,
            balanceWithdrawal = wallet.BalanceWithdrawal,
            totalBalance = wallet.Balance + wallet.BalanceBonus + wallet.BalanceWithdrawal,
            currency = wallet.Currency,
            updatedAt = wallet.UpdatedAt
        });
    }
    
    /// <summary>
    /// Rota para debitar o balanço do usuário.
    /// </summary>
    [HttpPost("DebitBalance")]
    public async Task<IActionResult> DebitBalance([FromBody] UpdateBalanceRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.token))
            return BadRequest(new { message = "Requisição inválida." });

        if (req.Amount <= 0)
            return BadRequest(new { message = "Valor deve ser maior que zero." });

        var user = await _sessionService.GetAsync(req.token);
        if (user == null)return Unauthorized();
        
        var _wallet = await _authService.GetAccount(user.UserId) as User;
        // Debug: Log do UserId
        Console.WriteLine($"[DEBUG] UserId: {user.UserId}, Email: {_wallet.Email}");

        try
        {
            var wallet = await _service.DebitAsync(_wallet.Id, req.Amount, req.type);
            var room = await _gameService.CreateGameRoom(user.UserId);
            return Ok(new 
            { 
                success = true,
                message = room.GameId,
                balance = wallet.Balance,
                balanceBonus = wallet.BalanceBonus,
                balanceWithdrawal = wallet.BalanceWithdrawal,
                totalBalance = wallet.Balance + wallet.BalanceBonus + wallet.BalanceWithdrawal,
                currency = wallet.Currency,
                updatedAt = wallet.UpdatedAt
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Erro interno ao processar débito.", error = ex.Message });
        }
    }

    /// <summary>
    /// Rota para creditar o balanço do usuário.
    /// </summary>
    [HttpPost("CreditBalance")]
    public async Task<IActionResult> CreditBalance([FromBody] UpdateBalanceRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.token))
            return BadRequest(new { message = "Requisição inválida." });

        if (req.Amount <= 0)
            return BadRequest(new { message = "Valor deve ser maior que zero." });

        var user = await _sessionService.GetAsync(req.token);
        Console.WriteLine($"{nameof(CreditBalance)} >> {user == null}");
        if (user == null) return Unauthorized();

        try
        {
            var wallet = await _service.CreditAsync(user.UserId, req.Amount, req.type);
            var withdraw = wallet.Balance - req.Amount;
            return Ok(new 
            { 
                success = true,
                message = "Crédito realizado com sucesso.",
                balance = wallet.Balance,
                balanceBonus = wallet.BalanceBonus,
                balanceWithdrawal = wallet.BalanceWithdrawal,
                totalBalance = wallet.Balance + wallet.BalanceBonus + wallet.BalanceWithdrawal,
                currency = wallet.Currency,
                updatedAt = wallet.UpdatedAt
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Erro interno ao processar crédito.", error = ex.Message });
        }
    }
}