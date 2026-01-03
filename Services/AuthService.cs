using DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Interfaces;

namespace Services;

public class AuthService
{
    private readonly IUserRepositorio<User> _repositorio;
    private readonly TokenService _tokenService;
    private readonly WalletService _walletService;
    private readonly SessionService _sessionService;
    private readonly UserRankingService _rankingService;
    private readonly IConfiguration _cfg;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AuthService(
        UserRankingService  rankingService,
        IUserRepositorio<User> repositorio,
        TokenService tokenService,
        WalletService walletService,
        SessionService sessionService,
        IConfiguration cfg,
        IPasswordHasher<User> passwordHasher)
    {
        _repositorio = repositorio;
        _rankingService =  rankingService;
        _tokenService = tokenService;
        _walletService = walletService;
        _sessionService = sessionService;
        _cfg = cfg;
        _passwordHasher = passwordHasher;

        _repositorio.InitializeCollection(_cfg["MongoDbSettings:ConnectionString"],
            _cfg["MongoDbSettings:DataBaseName"],
            "Users");
    }
    public async Task<TokenRequest?> AuthenticateAsync(string email, string password)
    {
        var user = await _repositorio.GetByMailAsync(
            email: email,
            none: CancellationToken.None);
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
    
    
    public async Task<TokenRequest?> RegisterAsync(
        string email,
        string password,
        string? code
    )
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            var existing = await _repositorio.GetByMailAsync(
                email: email,
                none: CancellationToken.None);
            if (existing != null) return null;

            var hashedPassword = _passwordHasher.HashPassword(new User(), password);

            var newUser = new User
            {
                Email = email,
                Name = email.Split('@')[0],
                PasswordHash = hashedPassword,
                CreatedAt = DateTime.UtcNow,
            };

            // Novo método específico para registrar afiliados
            var newUserId = await _repositorio.InsertOneAsync(
                new User
                {
                    Email = email,
                    IsInfluencer = false,
                    Name =  email.Split('@')[0],
                    PasswordHash = hashedPassword,
                    ReferralCode = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
                    Status = "active",
                    
                }
                
            );

            // Criar carteira
            var user = await _repositorio.GetByMailAsync(
                email: email,
                none: CancellationToken.None);
            if (user != null)
                await _walletService.GetOrCreateWalletAsync(user.Id);

            if (user == null)
                return null;

            var token = _tokenService.GenerateToken(user);
            await _sessionService.SetAsync(token, user);
            await _rankingService.InsertRanking(user);
            return new TokenRequest(token, user.IsInfluencer);
        }
        else
        {
            var user = await _repositorio.getUserByCode(code);
            if (user != null)
            {
                await _rankingService.UpdateRanking(user!);

                var existing = await _repositorio.GetByMailAsync(
                    email: email,
                    none: CancellationToken.None);
                if (existing != null) return null;

                var hashedPassword = _passwordHasher.HashPassword(new User(), password);

                var newUser = new User
                {
                    Email = email,
                    Name = email.Split('@')[0],
                    PasswordHash = hashedPassword,
                    CreatedAt = DateTime.UtcNow,
                };

                // Novo método específico para registrar afiliados
                var newUserId = await _repositorio.InsertOneAsync(
                    new User
                    {
                        Email = email,
                        IsInfluencer = false,
                        Name = email.Split('@')[0],
                        PasswordHash = hashedPassword,
                        registerCode = code,
                        ReferralCode = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
                        Status = "active",

                    }

                );

                // Criar carteira
                var _user = await _repositorio.GetByMailAsync(
                    email: email,
                    none: CancellationToken.None);
                if (user != null)
                    await _walletService.GetOrCreateWalletAsync(_user.Id);

                if (user == null)
                    return null;

                var token = _tokenService.GenerateToken(user);
                await _sessionService.SetAsync(token, user);

                return new TokenRequest(token, user.IsInfluencer);
            }
        }
        return null;
    }

    // ======================================================
    // GET ACCOUNT
    // ======================================================
    public async Task<object> GetAccount(Guid userId)
    {
        var user = await _repositorio.GetByIdAsync(
            id: userId,
            none: CancellationToken.None);
        return user ?? null;
    }
}
