using MongoDB.Bson.Serialization.Attributes;

namespace DTOs;

public class WalletLedger
{
    [BsonId]
    public Guid Id { get; set; } =  Guid.NewGuid();
    
    public bool IsInfluencer { get; set; } = false;
    public Guid WalletId { get; set; }
    public string Type { get; set; } 
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public long? GameRoundId { get; set; }
    public string Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
}