using Data;
using DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{

    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> Get(long id)
    {
        return Ok(new User());
    }
}