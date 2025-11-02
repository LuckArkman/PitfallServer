namespace DTOs;

public class StartRoundRequest
{
    public long UserId { get; set; }
    public string GameId { get; set; } = string.Empty;
}