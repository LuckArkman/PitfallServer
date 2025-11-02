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

        return Ok(result.Charge);
    }

    // ================================= PIX OUT =================================
    [HttpPost("withdraw")]
    public async Task<IActionResult> CreateWithdraw([FromBody] PixWithdrawRequestDto dto)
    {
        var user = await _session.GetAsync(dto.Token);
        if (user == null) return BadRequest(new { message = "Sessão inválida" });

        var wallet = await _wallet.GetOrCreateWalletAsync(user.UserId, null, null);
        Console.WriteLine($"{nameof(CreateWithdraw)} >> {wallet.Balance}");
        Console.WriteLine($"{nameof(CreateWithdraw)} >> {dto.Amount}");
        
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
    
    [HttpPost("deposit/status")]
    public async Task<IActionResult> GetPixStatus([FromBody] PixStatusRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.IdTransaction))
            return BadRequest(new { message = "IdTransaction é obrigatório." });

        var payload = new
        {
            token = "8243d189-abec-4ffe-b532-43e98566efb5",
            secret = "7a2ecf9f-af77-4814-949f-2b4c46253f46",
            idTransaction = dto.IdTransaction
        };

        var result = await _pixService.SendToFeiPayAsync<dynamic>("/wallet/deposit/status", payload);

        if (result == null)
            return StatusCode(502, new { message = "Erro ao consultar a Fei Pay." });

        return Ok(result);
    }


    // ================================= CALLBACK PIX-IN =================================
// Controllers/PixController.cs
    [HttpPost("callback")]
    public async Task<IActionResult> Callback([FromBody] PixWebhookDto callback)
    {
        Console.WriteLine(JsonSerializer.Serialize(callback));
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