using MongoDB.Bson.Serialization.Attributes;

namespace DTOs;

public class Order
{
    [BsonId]
    public string id { get; set; }

    [BsonElement("userId")]
    public string UserId { get; set; }

    [BsonElement("items")]
    public string transacao { get; set; } = "Pix_IN";

    [BsonElement("totalAmount")]
    public decimal TotalAmount { get; set; }

    [BsonElement("paymentMethod")]
    public string PaymentMethod { get; set; } // "CreditCard", "Pix", "Boleto", "money"

    [BsonElement("status")]
    public string Status { get; set; } // "Pending", "Paid", "Canceled"

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? updateAt { get; set; }
    
    
    public string? TransactionId { get; set; } = String.Empty;

    // --- NOVO: Dados persistidos para pagamento posterior ---
    public PaymentMetadata? PaymentData { get; set; } = null;
}