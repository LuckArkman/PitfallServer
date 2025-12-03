using System.ComponentModel.DataAnnotations;
using MongoDB.Bson.Serialization.Attributes;

namespace DTOs;

public class UserSession
{
    [Key] // Chave Primária
    [MaxLength(256)]
    public string SessionToken { get; set; }

    [Required]
    public Guid UserId { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public UserSession(string sessionToken, Guid userId, DateTime expiresAtUtc)
    {
        SessionToken = sessionToken;
        UserId = userId;
        ExpiresAtUtc = expiresAtUtc;
    }

    public UserSession()
    {
        
    }
}