namespace DTOs;

public class GameRound
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public decimal BetAmount { get; set; }
    public decimal PrizeAmount { get; set; }
    public string Result { get; set; } // mapear para enum game_result
    public int[] TrapPositions { get; set; }
    public int[] OpenedPositions { get; set; }
    public DateTime CreatedAt { get; set; }
}