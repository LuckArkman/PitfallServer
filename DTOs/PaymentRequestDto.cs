using System.Text.Json.Serialization;

namespace DTOs;

public class PaymentRequestDto
{
    public string id { get; set; } = Guid.NewGuid().ToString();
    [JsonPropertyName("token")]
    public string Token { get; set; } = null!;

    [JsonPropertyName("secret")]
    public string Secret { get; set; } = null!;

    [JsonPropertyName("postback")]
    public string Postback { get; set; } = null!;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("debtor_name")]
    public string DebtorName { get; set; } = null!;

    [JsonPropertyName("email")]
    public string Email { get; set; } = null!;

    [JsonPropertyName("debtor_document_number")]
    public string DebtorDocumentNumber { get; set; } = null!;

    [JsonPropertyName("phone")]
    public string Phone { get; set; } = null!;

    [JsonPropertyName("method_pay")]
    public string MethodPay { get; set; } = null!;

    [JsonPropertyName("split_email")]
    public string SplitEmail { get; set; } = null!;

    [JsonPropertyName("split_percentage")]
    public decimal SplitPercentage { get; set; }
}