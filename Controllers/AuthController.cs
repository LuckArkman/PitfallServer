using Microsoft.AspNetCore.Mvc;
using Services;

namespace Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    public AuthController(AuthService auth) => _auth = auth;

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
    
    [HttpPost("Register")]
    public async Task<IActionResult> Register([FromBody] LoginRequest req)
    {
        var token = await _auth.RegisterAsync(req.Email, req.Password);
        if (token == null)
        {
            Console.WriteLine($"{nameof(Register)} >> Credenciais inválidas. ! {req.Email}");
            return Ok(new { message = "Credenciais inválidas." });
        }

        Console.WriteLine($"{nameof(Register)} >> Usuario registrado com Sucesso ! {req.Email}");
        return Ok(new { token });
    }
}

public class LoginRequest { public string Email { get; set; } public string Password { get; set; } }