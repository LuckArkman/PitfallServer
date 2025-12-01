using System.ComponentModel.DataAnnotations;

namespace DTOs;

public class _User
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
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
    public string Code { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public Guid? InviterL1 { get; set; }
    public Guid? InviterL2 { get; set; }
    public Guid? InviterL3 { get; set; }
}