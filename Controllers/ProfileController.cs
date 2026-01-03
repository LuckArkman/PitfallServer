using DTOs;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{
    readonly ProfileService _service;
    readonly SessionService _sessionService;
    public ProfileController(ProfileService service,
        SessionService sessionService)
    {
        _service = service;
        _sessionService = sessionService;
    }
    [HttpPost("profile")]
    public async Task<IActionResult?> ProfileAccount([FromBody] RequestWallet req)
    {
        var user = await _sessionService.GetAsync(req.token) as UserSession;
        var profile = await _service.GetProfile(user.UserId);
        return Ok(profile);
    }
    [HttpPost("Invite-profile")]
    public async Task<IActionResult?> Invite_ProfileAccount([FromBody] RequestWallet req)
    {
        var user = await _sessionService.GetAsync(req.token) as UserSession;
        var profile = await _service.GetInvite_Profile(user.UserId);
        return Ok(profile);
    }
}