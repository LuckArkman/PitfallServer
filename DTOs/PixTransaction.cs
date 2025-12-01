namespace DTOs;

public class PixTransaction
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = "PIX_IN";
    public string IdTransaction { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = "pending";
    public string PixKey { get; set; } = string.Empty;
    public string PixKeyType { get; set; } = string.Empty;
    public string QrCode { get; set; } = string.Empty;
    public string QrCodeImageUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; } 
}