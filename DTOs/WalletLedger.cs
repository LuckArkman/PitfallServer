namespace DTOs;

public class WalletLedger
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Type { get; set; } // mapear para entry_type
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public long? GameRoundId { get; set; }
    public string Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
}