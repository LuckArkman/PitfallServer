namespace DTOs;

public class RoundDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Result { get; set; } // "win" | "lose" | "cashout"
    public decimal BetAmount { get; set; }
    public decimal? PrizeTotal { get; set; }
}