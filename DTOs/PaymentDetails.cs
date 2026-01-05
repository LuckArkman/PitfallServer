namespace DTOs;

public class PaymentDetails
{
    // Para PIX
    public string? PixQrCode { get; set; }          // Código PIX (string para copiar)
    public string? PixQrCodeImage { get; set; }     // Base64 da imagem QR Code
    public DateTime? PixExpirationDate { get; set; }

    // Para Boleto
    public string? BoletoBarCode { get; set; }      // Código de barras
    public string? BoletoPdfUrl { get; set; }       // URL para download do PDF
    public DateTime? BoletoDueDate { get; set; }    // Data de vencimento
    public decimal? BoletoAmount { get; set; }
    
    // Comum
    public string? PaymentMethod { get; set; }
}
