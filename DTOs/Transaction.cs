using System.Text.Json.Serialization;

namespace DTOs;

public class Transaction
{
    [JsonPropertyName("typeTransaction")]
    public string TypeTransaction { get; set; }

    [JsonPropertyName("statusTransaction")]
    public string StatusTransaction { get; set; }

    [JsonPropertyName("idTransaction")]
    public string IdTransaction { get; set; }

    [JsonPropertyName("e2d")]
    public string E2d { get; set; }

    [JsonPropertyName("paid_by")]
    public string PaidBy { get; set; }

    [JsonPropertyName("paid_doc")]
    public string PaidDoc { get; set; }
}