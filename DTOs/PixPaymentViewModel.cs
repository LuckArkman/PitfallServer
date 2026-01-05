namespace DTOs;

public class PixPaymentViewModel
{
    public string TransactionId { get; set; } = string.Empty;
    public string PixCode { get; set; } = string.Empty;
    public string QrCodeBase64 { get; set; } = string.Empty;
    public DateTime ExpirationDate { get; set; }
    public decimal Amount { get; set; }
    public string OrderId { get; set; } = string.Empty;
}