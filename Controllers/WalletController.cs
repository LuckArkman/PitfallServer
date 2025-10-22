using DTOs; 
using Microsoft.AspNetCore.Mvc;
using Services;

namespace Controllers;

[ApiController]
[Route("api/[controller]")]
public class WalletController : ControllerBase
{
    readonly WalletService  _service;
    readonly SessionService  _sessionService;
    public WalletController(WalletService service,
        SessionService sessionService)
    {
        _service = service;
        _sessionService = sessionService;
    }

    [HttpPost("wallet")]
    public async Task<IActionResult> wallet([FromBody] RequestWallet req)
    {
        var user = await _sessionService.GetAsync<User>(req.token);
        if (user != null)
        {
            var wallet = await _service.GetOrCreateWalletAsync(user.Id);
            return Ok(wallet.Balance);
        }
        return NotFound();
    }
}