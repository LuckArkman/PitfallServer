namespace DTOs;

public class RequestTransaction
{
    public string id { get; set; } = Guid.NewGuid().ToString();
    public Guid userId { get; set; }
    public Guid walletId { get; set; }
    public  string name { get; set; }
    public string email{ get; set; }
    public string document{ get; set; }
    public decimal amount { get; set; }
    public string Status { get; set; } = "pending";
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; } 
}