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

    public PixController(PixService pixService, SessionService session, WalletService wallet)
    {
        _pixService = pixService;
        _session = session;
        _wallet = wallet;
    }

    // ================================= PIX IN =================================
    [HttpPost("deposit")]
    public async Task<IActionResult> CreateDeposit([FromBody] PixDepositRequestDto dto)
    {
        var user = await _session.GetAsync<User>(dto.Token);
        if (user == null) return BadRequest(new { message = "Sessão inválida" });

        var pixReq = new PixDepositRequest(dto.Amount, dto.Name, dto.Email, dto.Document, dto.Phone, dto.SplitEmail, dto.SplitPercentage);
        var result = await _pixService.CreatePixDepositAsync(pixReq);

        return Ok(new
        {
            transactionId = result?.IdTransaction,
            qrcode = result?.Qrcode,
            qrImage = result?.Qr_Code_Image_Url
        });
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
    [HttpPost("callback")]
    public async Task<IActionResult> Callback([FromBody] PixWebhookDto callback)
    {
        // Quando status = paid → creditar usuário
        if (callback.status == "paid")
        {
            await _wallet.CreditAsync(callback.userId, callback.amount, "PIX_IN");
        }

        return Ok(new { message = "Webhook recebido com sucesso." });
    }
}