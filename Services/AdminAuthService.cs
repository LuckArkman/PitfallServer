using System.Security.Cryptography;
using System.Text;
using Data;
using DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Services;

    /// <summary>
    /// Serviço responsável pela autenticação de administradores e emissão de tokens JWT.
    /// </summary>
    public class AdminAuthService
    {
        private readonly AdminTokenService _tokenService;
        private AdminRepository _adminRepository;
        private readonly IConfiguration _cfg;

        public AdminAuthService(AdminTokenService tokenService,
            IConfiguration  config)
        {
            _tokenService = tokenService;
            _cfg = config;
            _adminRepository = new AdminRepository(_cfg["ConnectionStrings:DefaultConnection"]);
        }

        /// <summary>
        /// Autentica um administrador com base em e-mail e senha, gerando um token JWT se válido.
        /// </summary>
        /// <param name="email">E-mail do administrador</param>
        /// <param name="password">Senha em texto puro</param>
        /// <returns>Token JWT se autenticado com sucesso, ou null se falhar</returns>
        public async Task<string> AuthenticateAsync(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return null;

            var admin = await _adminRepository.GetByEmailAsync(email);
            if (admin == null)
                return null;

            // Verifica hash da senha
            if (!VerifyPassword(password, admin.PasswordHash))
                return null;

            // Atualiza data de último login
            admin.LastLoginAt = DateTime.UtcNow;

            // Gera o token JWT
            return _tokenService.GenerateToken(admin);
        }

        /// <summary>
        /// Registra um novo administrador com e-mail e senha hashada.
        /// </summary>
        public async Task<Admin> RegisterAsync(string email, string password)
        {
            var adm =  await _adminRepository.GetByEmailAsync(email);
            if (adm != null) throw new InvalidOperationException("E-mail já cadastrado para outro administrador.");

            var admin = new Admin
            {
                Email = email,
                PasswordHash = ComputeSha256Hash(password),
                CreatedAt = DateTime.UtcNow,
                Role = "Administrator"
            };

            await _adminRepository.CreateAdminAsync(email, password, "Administrator");

            return admin;
        }

        /// <summary>
        /// Verifica se o hash da senha coincide com o valor armazenado.
        /// </summary>
        private static bool VerifyPassword(string password, string storedHash)
        {
            var hash = ComputeSha256Hash(password);
            return string.Equals(hash, storedHash, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gera o hash SHA256 de uma senha.
        /// (Para produção, recomenda-se trocar por BCrypt ou Argon2)
        /// </summary>
        private static string ComputeSha256Hash(string raw)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            var sb = new StringBuilder();
            foreach (var b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }