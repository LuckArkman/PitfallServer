namespace DTOs;

public class PaymentMetadata
{
    // PIX
    public string? PixQrCode { get; set; }          // "Copia e Cola"
    public string? PixQrCodeBase64 { get; set; }    // Imagem do QR
    public DateTime? ExpirationDate { get; set; }

    // Boleto
    public string? BoletoBarcode { get; set; }      // Linha digitável
    public string? BoletoPdfUrl { get; set; }       // Link do PDF
    public DateTime? BoletoDueDate { get; set; }    // Vencimento
}