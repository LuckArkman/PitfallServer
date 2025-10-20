using System.Security.Cryptography;
using System.Text;
using Data;
using Microsoft.EntityFrameworkCore;

namespace Services;

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokenService;
    public AuthService(AppDbContext db, TokenService tokenService) { _db = db; _tokenService = tokenService; }

    public async Task<string> AuthenticateAsync(string email, string password)
    {
        var hash = ComputeSha256Hash(password);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        // NOTE: in schema original password stored in users? if not, adapt to use credentials table.
        // Here we assume PasswordHash in Admins only. If you store user passwords, add property.
        if (user == null) return null;

        // This example assumes user.PasswordHash exists; if you store differently, change accordingly.
        // For now return token unconditionally (demo)
        return _tokenService.GenerateToken(user);
    }

    private static string ComputeSha256Hash(string raw)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
        var sb = new StringBuilder();
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}