using System.Text.Json.Serialization;

namespace DTOs;

public class PixResponse
{
    [JsonPropertyName("statusCode")]
    public int statusCode { get; set; }

    [JsonPropertyName("id")]
    public string id { get; set; } = string.Empty;

    [JsonPropertyName("pix")]
    public string pix { get; set; } = string.Empty;

    // "value" vem como string no JSON ("5.00") — usamos um conversor para decimal
    [JsonPropertyName("value")]
    [JsonConverter(typeof(StringToDecimalConverter))]
    public decimal value { get; set; }

    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))] // desserializa "PENDING" para o enum
    public PixStatus status { get; set; }

    [JsonPropertyName("acquirer_used")]
    public string acquirer_used { get; set; } = string.Empty;
}