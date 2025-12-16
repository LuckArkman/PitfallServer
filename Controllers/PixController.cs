using System.Text;
using Microsoft.AspNetCore.Mvc;
using Services;
using DTOs;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

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
        var session = await _session.GetAsync(dto.token);
        if (session == null) return BadRequest(new { message = "Sessão inválida" });
        var user = await _authService.GetAccount(session.UserId) as User;
        var pixReq = new PixDepositRequest(dto.amount, user.Name, user.Email, dto.documentNumber, dto.phone);
        var result = await _pixService.CreatePixDepositAsync(pixReq, user);
        return Ok(result);
    }
    [HttpPost("withdraw")]
    public async Task<IActionResult> CreateWithdraw([FromBody] PixWithdrawRequestDto dto)
    {
        var session = await _session.GetAsync(dto.Token);
        if (session == null) return BadRequest(new { message = "Sessão inválida" });

        var wallet = await _wallet.GetOrCreateWalletAsync(session.UserId);
        Console.WriteLine($"{nameof(CreateWithdraw)} >> {wallet.Balance}");
        Console.WriteLine($"{nameof(CreateWithdraw)} >> {dto.Amount}");
        var user = await _authService.GetAccount(session.UserId) as User;
        if (wallet.BalanceWithdrawal < dto.Amount)
            return BadRequest(new { message = "Saldo insuficiente para saque" });
        var result = await _pixService.CreatePixWithdrawAsync(new PixWithdrawRequest(dto.Amount, dto.PixKey, dto.PixKeyType), user);

        return Ok(new
        {
            success = true,
            id = result?.Id, 
            status = result?.WithdrawStatusId, 
            amount = result?.Amount
        });
    }

    // ================================= CALLBACK PIX-IN (WEBHOOK) =================================
    [HttpPost("callback")]
    public async Task<IActionResult> Callback([FromBody] PaymentStatusDto callback)
    {
        if (callback == null)
            return BadRequest(new { message = "Payload inválido." });

        try
        {
            var processed = await _pixService.ProcessWebhookAsync(callback);
            if(processed) Console.WriteLine($"Webhook processado com sucesso.{callback.IdTransaction}");

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