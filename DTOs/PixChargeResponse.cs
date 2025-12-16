using System.Text.Json.Serialization;

namespace DTOs;

public class PixChargeResponse
{
    [JsonPropertyName("idTransaction")]
    public string IdTransaction { get; set; } = null!;

    [JsonPropertyName("qrcode")]
    public string QrCode { get; set; } = null!;

    [JsonPropertyName("qr_code_image_url")]
    public string QrCodeImageUrl { get; set; } = null!;

    [JsonPropertyName("charge")]
    public Charge Charge { get; set; } = null!;
}