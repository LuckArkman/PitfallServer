using System.Text.Json.Serialization;

namespace DTOs;

public class StormPagCashInResponse
{
    // Mapeamento da resposta da StormPag
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }
    
    [JsonPropertyName("id")] // ID da transação
    public string Id { get; set; }
    
    [JsonPropertyName("pix")] // String do QR Code / Linha digitável
    public string Pix { get; set; }
    
    [JsonPropertyName("value")]
    public decimal Value { get; set; }
    
    [JsonPropertyName("status")]
    public string Status { get; set; }
}