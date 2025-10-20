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
        if (token == null) return Ok(new { message = "Credenciais inválidas." });
        return Ok(new { token });
    }
}

public class LoginRequest { public string Email { get; set; } public string Password { get; set; } }