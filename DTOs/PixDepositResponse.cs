using System.Text.Json.Serialization;

namespace DTOs
{
    public class PixDepositResponse
    {
        [JsonPropertyName("idTransaction")]
        public string IdTransaction { get; set; } = string.Empty;

        [JsonPropertyName("qrcode")]
        public string QrCode { get; set; } = string.Empty;

        [JsonPropertyName("qr_code_image_url")]
        public string QrCodeImageUrl { get; set; } = string.Empty;

        [JsonPropertyName("charge")]
        public PixCharge Charge { get; set; } = new();
        
    }
}