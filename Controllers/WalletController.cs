using DTOs;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace Controllers;

[ApiController]
[Route("api/[controller]")]
public class WalletController : ControllerBase
{
    readonly WalletService _service;
    readonly SessionService _sessionService;

    public WalletController(WalletService service, SessionService sessionService)
    {
        _service = service;
        _sessionService = sessionService;
    }

    [HttpPost("wallet")]
    public async Task<IActionResult> GetWallet([FromBody] RequestWallet req)
    {
        if (string.IsNullOrWhiteSpace(req.token))
            return BadRequest(new { message = "Token não fornecido." });

        var user = await _sessionService.GetAsync<User>(req.token);
        if (user == null)
            return Ok(new { message = "Sessão inválida ou expirada." });

        var wallet = await _service.GetOrCreateWalletAsync(user.Id);

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

        var user = await _sessionService.GetAsync<User>(req.token);
        if (user == null)
            // ATUALIZAÇÃO: Alterado de Ok() para Unauthorized() para refletir o status correto de um token inválido.
            return Unauthorized();

        // Debug: Log do UserId
        Console.WriteLine($"[DEBUG] UserId: {user.Id}, Email: {user.Email}");

        try
        {
            var wallet = await _service.DebitAsync(user.Id, req.Amount, req.type);
            
            return Ok(new 
            { 
                success = true,
                message = "Débito realizado com sucesso.",
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

        var user = await _sessionService.GetAsync<User>(req.token);
        Console.WriteLine($"{nameof(CreditBalance)} >> {user == null}");
        if (user == null)
            return Unauthorized();

        try
        {
            var wallet = await _service.CreditAsync(user.Id, req.Amount, req.type);
            
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