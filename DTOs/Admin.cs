namespace DTOs;

public class Admin
{
    public long Id { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public string Role { get; set; } = "Administrator";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}