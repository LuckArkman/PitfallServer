using System.ComponentModel.DataAnnotations;

namespace DTOs;

public class User
{
    public long Id { get; set; }
    [MaxLength(255)] public string Email { get; set; }
    public string Name { get; set; }
    public bool IsInfluencer { get; set; }
    public string Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navegação
    public Wallet Wallet { get; set; }
}