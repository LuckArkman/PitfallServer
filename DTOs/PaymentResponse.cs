namespace DTOs;

public class PaymentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public string? TransactionId { get; set; }
    public PaymentDetails? Details { get; set; }
}