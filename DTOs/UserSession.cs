using System.ComponentModel.DataAnnotations;

namespace DTOs;

public class UserSession
{
    [Key] // Chave Primária
    [MaxLength(256)]
    public string SessionToken { get; set; }

    [Required]
    public long UserId { get; set; }

    public DateTime ExpiresAtUtc { get; set; }
}