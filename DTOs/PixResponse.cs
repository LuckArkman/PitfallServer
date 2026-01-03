using System.Text.Json.Serialization;

namespace DTOs;

public class PixResponse
{
    public string? location { get; set; }
    public string correlationId { get; set; } = "";
    public string txid { get; set; } = "";
    public string status { get; set; } = "";
    public string chave { get; set; } = "";
    public string pixCopiaECola { get; set; } = "";
    public string qrCode { get; set; } = "";
}