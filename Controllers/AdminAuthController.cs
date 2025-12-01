using Microsoft.AspNetCore.Mvc;
using Services;

namespace Controllers;

[ApiController]
[Route("api/admin")]
public class AdminAuthController : ControllerBase
{
    private readonly AdminAuthService _auth;

    public AdminAuthController(AdminAuthService auth)
    {
        _auth = auth;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var token = await _auth.AuthenticateAsync(req.Email, req.Password);
        if (token == null)
            return Ok(new { message = "Credenciais inválidas." });

        return Ok(new { token });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] LoginRequest req)
    {
        var admin = await _auth.RegisterAsync(req.Email, req.Password);
        return Ok(new { admin.Id, admin.Email, admin.Role });
    }
}