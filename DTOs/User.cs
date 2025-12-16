using System.ComponentModel.DataAnnotations;
using MongoDB.Bson.Serialization.Attributes;

namespace DTOs
{
    public class User
    {
        [BsonId]
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Email { get; set; }

        [Required, MaxLength(255)]
        public string Name { get; set; }

        [Required]
        public string PasswordHash { get; set; } 

        public bool IsInfluencer { get; set; } = false;

        [MaxLength(50)]
        public string Status { get; set; } = "active";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? registerCode { get; set; } = string.Empty;
        public string? ReferralCode { get; set; }
        // Navegação
        public Wallet Wallet { get; set; }
    }
}