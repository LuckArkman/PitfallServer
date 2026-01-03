using System.Text.Json.Serialization;

namespace DTOs;

public class Charge
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("qrCode")]
    public string QrCode { get; set; } = null!;

    [JsonPropertyName("brCode")]
    public string BrCode { get; set; } = null!;
}
