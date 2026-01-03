namespace DTOs;

public class StartRoundRequest
{
    public Guid UserId { get; set; }
    public string GameId { get; set; } = string.Empty;
}