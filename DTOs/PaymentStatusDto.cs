using System.Text.Json.Serialization;

namespace DTOs;

public class PaymentStatusDto
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = null!;

    [JsonPropertyName("idTransaction")]
    public string IdTransaction { get; set; } = null!;

    [JsonPropertyName("typeTransaction")]
    public string TypeTransaction { get; set; } = null!;
}