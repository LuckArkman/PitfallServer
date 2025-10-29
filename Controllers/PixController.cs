using Microsoft.AspNetCore.Mvc;
using Services;
using DTOs;

namespace Controllers;

[ApiController]
[Route("api/[controller]")]
public class PixController : ControllerBase
{
    private readonly PixService _pixService;
    private readonly SessionService _session;
    private readonly WalletService _wallet;
    readonly AuthService  _authService;

    public PixController(PixService pixService,
        SessionService session,
        WalletService wallet,
        AuthService authService)
    {
        _pixService = pixService;
        _session = session;
        _wallet = wallet;
        _authService = authService;
    }

    // ================================= PIX IN =================================
    [HttpPost("deposit")]
    public async Task<IActionResult> CreateDeposit([FromBody] PixDepositRequestDto dto)
    {
        Console.WriteLine($"{nameof(CreateDeposit)} >> {dto == null}");
        var user = await _session.GetAsync<User>(dto.token);
        if (user == null) return BadRequest(new { message = "Sessão inválida" });
        var pixReq = new PixDepositRequest(dto.amount, user.Name, user.Email, dto.documentNumber, dto.phone);
        var result = await _pixService.CreatePixDepositAsync(pixReq, null);

        return Ok(result.Charge);
    }

    // ================================= PIX OUT =================================
    [HttpPost("withdraw")]
    public async Task<IActionResult> CreateWithdraw([FromBody] PixWithdrawRequestDto dto)
    {
        var user = await _session.GetAsync<User>(dto.Token);
        if (user == null) return BadRequest(new { message = "Sessão inválida" });

        var wallet = await _wallet.GetOrCreateWalletAsync(user.Id);
        if (wallet.BalanceWithdrawal < dto.Amount)
            return BadRequest(new { message = "Saldo insuficiente para saque" });

        var result = await _pixService.CreatePixWithdrawAsync(new PixWithdrawRequest(dto.Amount, dto.PixKey, dto.PixKeyType));

        return Ok(new
        {
            success = true,
            id = result?.Id,
            status = result?.WithdrawStatusId,
            amount = result?.Amount
        });
    }
    // ================================= CALLBACK PIX-IN =================================
// Controllers/PixController.cs
    [HttpPost("callback")]
    public async Task<IActionResult> Callback([FromBody] PixWebhookDto callback)
    {
        if (callback == null)
            return BadRequest(new { message = "Payload inválido." });

        try
        {
            var processed = await _pixService.ProcessWebhookAsync(callback);

            return Ok(new
            {
                message = processed ? "Webhook processado com sucesso." : "Transação não encontrada ou inválida.",
                processed
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WEBHOOK ERRO] {ex.Message}\n{ex.StackTrace}");
            return StatusCode(500, new { message = "Erro interno ao processar webhook." });
        }
    }
}