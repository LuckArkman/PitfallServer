namespace DTOs;

public class UpdateBalanceRequest
{
    public string token { get; set; }
    public decimal Amount { get; set; }
    public string type { get; set; }
}