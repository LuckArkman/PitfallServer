namespace DTOs;

public class BoletoPaymentViewModel
{
    public string TransactionId { get; set; } = string.Empty;
    public string BarCode { get; set; } = string.Empty;
    public string BoletoNumber { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public decimal Amount { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string PdfDownloadUrl { get; set; } = string.Empty;
}