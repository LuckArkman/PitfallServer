using System.Text.Json.Serialization;

namespace DTOs;

public class PixCharge
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("qrCode")]
    public string QrCodeImageUrl { get; set; }

    [JsonPropertyName("brCode")]
    public string BrCode { get; set; }
}