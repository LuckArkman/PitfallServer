namespace DTOs;

public class FeiPayResponse
{
    public string idTransaction { get; set; } = string.Empty;
    public string status { get; set; } = string.Empty;
    public decimal amount { get; set; }
    public string? paid_at { get; set; }
}
