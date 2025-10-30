namespace DTOs;

public class Wallet
{
    public long UserId { get; set; }
    public string Currency { get; set; } = "BRL";
    public decimal Balance { get; set; }
    public decimal BalanceWithdrawal { get; set; }
    public decimal BalanceBonus { get; set; }
    public DateTime UpdatedAt { get; set; }
}