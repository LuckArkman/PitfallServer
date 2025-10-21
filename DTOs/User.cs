using System.ComponentModel.DataAnnotations;

namespace DTOs
{
    public class User
    {
        public long Id { get; set; }

        [Required, MaxLength(255)]
        public string Email { get; set; }

        [Required, MaxLength(255)]
        public string Name { get; set; }

        [Required]
        public string PasswordHash { get; set; }  // 🔒 necessário para autenticação

        public bool IsInfluencer { get; set; } = false;

        [MaxLength(50)]
        public string Status { get; set; } = "active";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navegação
        public Wallet Wallet { get; set; }
    }
}