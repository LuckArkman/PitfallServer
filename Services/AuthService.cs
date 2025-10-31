using System.Security.Cryptography;
using System.Text;
using Data;
using Data.Repositories;
using DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Services;

public class AuthService
{
    private PostgresUserRepository _postgresUserRepository;
    readonly TokenService _tokenService;
    readonly WalletService _walletService;
    readonly SessionService  _sessionService;
    private readonly IConfiguration _cfg;
    public AuthService(
        TokenService tokenService,
        WalletService walletService,
        SessionService sessionService,
        IConfiguration cfg)
    {
        _tokenService = tokenService;
        _walletService = walletService;
        _sessionService = sessionService;
        _cfg = cfg;
        
        _postgresUserRepository = new PostgresUserRepository(_cfg["ConnectionStrings:DefaultConnection"]);
    }

    public async Task<string> AuthenticateAsync(string email, string password)
    {
        var user = await _postgresUserRepository.GetByEmailAsync(email);
        if (user == null || password != user.PasswordHash) return null;
        
        
        var token = _tokenService.GenerateToken(user);
        await _sessionService.SetAsync(token, user);
        return token;
    }
    
    public async Task<string?> RegisterAsync(string email, string password)
    {
        var _user = await _postgresUserRepository.GetByEmailAsync(email);
        if (_user != null) return null;

        var newUser = new User
        {
            Email = email,
            Name = email.Split('@')[0],
            PasswordHash = password,
            CreatedAt = DateTime.UtcNow
        };

        await _postgresUserRepository.RegisterAsync(newUser.Email, newUser.Name, newUser.PasswordHash);
        var user = await _postgresUserRepository.GetByEmailAsync(email);
        if (user != null)
        {
            var wallet = await _walletService.GetOrCreateWalletAsync(user.Id, null, null );
        }

        return _tokenService.GenerateToken(newUser);
    }

    public async Task<object> GetAccount(long userId)
    {
        var user = await _postgresUserRepository.GetByIdAsync(userId);
        // NOTE: in schema original password stored in users? if not, adapt to use credentials table.
        // Here we assume PasswordHash in Admins only. If you store user passwords, add property.
        if (user == null) return null;
        return user;
    }
}