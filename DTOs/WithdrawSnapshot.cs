namespace DTOs;

public class WithdrawSnapshot
{
    public Guid _id { get; set; } = Guid.NewGuid();
    public Guid? _walletId { get; set; }
    public string? _gameRoom  { get; set; }
    
    public decimal _originalBalance { get; set; }
    public decimal _balanceWithdrawal { get; set; }
    public decimal _balanceBonus { get; set; }
    public DateTime _createdAt { get; set; }
}