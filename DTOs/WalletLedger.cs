namespace DTOs;

public class WalletLedger
{
    public WalletLedger(){}
    public Guid Id { get; set; } =  Guid.NewGuid();
    public Guid WalletId { get; set; }
    public string Type { get; set; } 
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public long? GameRoundId { get; set; }
    public string Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
}