using Microsoft.AspNetCore.Mvc;
using Services;
using DTOs;

namespace Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly ReferralService _referral;

    public AuthController(AuthService auth, ReferralService referral)
    {
        _auth = auth;
        _referral = referral;
    }

    // ============================================
    // LOGIN
    // ============================================
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var token = await _auth.AuthenticateAsync(req.Email, req.Password);
        if (token == null)
        {
            Console.WriteLine($"{nameof(Login)} >> Credenciais inválidas. ! {req.Email}");
            return Ok(null);
        }

        Console.WriteLine($"{nameof(Login)} >> Usuario Logado com Sucesso ! {req.Email}");
        return Ok(new { token });
    }


    // ============================================
    // REGISTER COM SUPORTE A REFERRAL
    // ============================================
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        // ----------------------------------------------------
        // 1. Recuperar o REF (body ou cookie)
        // ----------------------------------------------------
        string? referralCode = req.RefCode;

        if (string.IsNullOrWhiteSpace(referralCode))
        {
            referralCode = Request.Cookies["ref_code"];
        }

        Guid? l1 = null;
        Guid? l2 = null;
        Guid? l3 = null;

        // ----------------------------------------------------
        // 2. Obter cadeia de afiliados
        // ----------------------------------------------------
        if (!string.IsNullOrWhiteSpace(referralCode))
        {
            var chain = await _referral.AttachReferralChainFromRefAsync(referralCode);
            l1 = chain.inviterL1;
            l2 = chain.inviterL2;
            l3 = chain.inviterL3;
        }

        // ----------------------------------------------------
        // 3. Registrar usuário com cadeia de uplines
        // ----------------------------------------------------
        var result = await _auth.RegisterAsync(
            email: req.Email,
            password: req.Password,
            code: req.RefCode
        );

        if (result == null)
        {
            Console.WriteLine($"{nameof(Register)} >> Falha ao registrar ! {req.Email}");
            return BadRequest(new { message = "Falha ao registrar usuário." });
        }

        Console.WriteLine($"{nameof(Register)} >> Usuário registrado com sucesso ! {req.Email}");
        return Ok(new { token = result });
    }
}


// =========================================
// REQUESTS
// =========================================

public class LoginRequest
{
    public string Email { get; set; }
    public string Password { get; set; }
}

public class RegisterRequest
{
    public string Email { get; set; }
    public string Password { get; set; }

    // opcional: enviado pelo front
    public string? RefCode { get; set; }
}
