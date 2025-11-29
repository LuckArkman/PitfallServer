using Data.Repositories;
using DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace Services;

public class AuthService
{
    private readonly PostgresUserRepository _postgresUserRepository;
    private readonly TokenService _tokenService;
    private readonly WalletService _walletService;
    private readonly SessionService _sessionService;
    private readonly IConfiguration _cfg;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AuthService(
        TokenService tokenService,
        WalletService walletService,
        SessionService sessionService,
        IConfiguration cfg,
        IPasswordHasher<User> passwordHasher)
    {
        _tokenService = tokenService;
        _walletService = walletService;
        _sessionService = sessionService;
        _cfg = cfg;
        _passwordHasher = passwordHasher;

        _postgresUserRepository =
            new PostgresUserRepository(_cfg["ConnectionStrings:DefaultConnection"]);
    }

    // ======================================================
    // LOGIN
    // ======================================================
    public async Task<TokenRequest?> AuthenticateAsync(string email, string password)
    {
        var user = await _postgresUserRepository.GetByEmailAsync(email);
        if (user == null) return null;

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);

        if (result == PasswordVerificationResult.Success)
        {
            var token = _tokenService.GenerateToken(user);
            await _sessionService.SetAsync(token, user);
            return new TokenRequest(token, user.IsInfluencer);
        }

        return null;
    }

    // ======================================================
    // REGISTER COM SUPORTE A 3 NÍVEIS DE AFILIADOS
    // ======================================================
    public async Task<TokenRequest?> RegisterAsync(
        string email,
        string password,
        Guid? inviterL1,
        Guid? inviterL2,
        Guid? inviterL3
    )
    {
        var existing = await _postgresUserRepository.GetByEmailAsync(email);
        if (existing != null) return null;

        var hashedPassword = _passwordHasher.HashPassword(new User(), password);

        var newUser = new User
        {
            Email = email,
            Name = email.Split('@')[0],
            PasswordHash = hashedPassword,
            CreatedAt = DateTime.UtcNow,
            InviterL1 = inviterL1,
            InviterL2 = inviterL2,
            InviterL3 = inviterL3
        };

        // Novo método específico para registrar afiliados
        var newUserId = await _postgresUserRepository.RegisterAsync(
            newUser.Email,
            newUser.Name,
            newUser.PasswordHash,
            inviterL1,
            inviterL2, 
            inviterL3 
        );

        // Criar carteira
        var user = await _postgresUserRepository.GetByEmailAsync(email);
        if (user != null)
            await _walletService.GetOrCreateWalletAsync(user.Id, null, null);

        if (user == null)
            return null;

        var token = _tokenService.GenerateToken(user);
        await _sessionService.SetAsync(token, user);

        return new TokenRequest(token, user.IsInfluencer);
    }

    // ======================================================
    // GET ACCOUNT
    // ======================================================
    public async Task<object> GetAccount(long userId)
    {
        var user = await _postgresUserRepository.GetByIdAsync(userId);
        return user ?? null;
    }
}
