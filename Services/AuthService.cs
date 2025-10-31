using Data.Repositories;
using DTOs;
using Microsoft.AspNetCore.Identity; 
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace Services;

public class AuthService
{
    private PostgresUserRepository _postgresUserRepository;
    readonly TokenService _tokenService;
    readonly WalletService _walletService;
    readonly SessionService _sessionService;
    private readonly IConfiguration _cfg;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AuthService(
        TokenService tokenService,
        WalletService walletService,
        SessionService sessionService,
        IConfiguration cfg,
        IPasswordHasher<User> passwordHasher) // Injeção de dependência do hasher
    {
        _tokenService = tokenService;
        _walletService = walletService;
        _sessionService = sessionService;
        _cfg = cfg;
        _passwordHasher = passwordHasher; // Atribuição do hasher
        
        // Mantenha o repositório por enquanto, mas considere injetar a interface IPostgresUserRepository
        _postgresUserRepository = new PostgresUserRepository(_cfg["ConnectionStrings:DefaultConnection"]);
    }

    public async Task<string> AuthenticateAsync(string email, string password)
    {
        var user = await _postgresUserRepository.GetByEmailAsync(email);
        
        if (user == null) 
            return null;
        
        // 🔑 Verificação de senha usando o hash salvo
        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        
        if (result == PasswordVerificationResult.Success)
        {
            // Senha correta: procede com a autenticação
            var token = _tokenService.GenerateToken(user);
            await _sessionService.SetAsync(token, user);
            return token;
        }
        
        // Senha incorreta
        return null;
    }
    
    public async Task<string?> RegisterAsync(string email, string password)
    {
        var _user = await _postgresUserRepository.GetByEmailAsync(email);
        if (_user != null) return null;

        // 🔑 AQUI ESTÁ O PASSO CRÍTICO: Usar o PasswordHasher para gerar o hash
        var hashedPassword = _passwordHasher.HashPassword(new User(), password);

        var newUser = new User
        {
            Email = email,
            Name = email.Split('@')[0],
            PasswordHash = hashedPassword, // Armazena o HASH seguro (hash + salt empacotados)
            CreatedAt = DateTime.UtcNow
        };

        // Chama o repositório com o hash seguro
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
        if (user == null) return null;
        return user;
    }
}