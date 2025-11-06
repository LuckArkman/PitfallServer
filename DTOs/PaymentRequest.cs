namespace DTOs;

public class PaymentRequest
{
    public string code { get; set; }
    public decimal amount { get; set; }
    public string email { get; set; }
    public string document { get; set; }
    public string url { get; set; }
    
    public PaymentRequest(string code, decimal amount, string email, string document, string url)
    {
        this.code = code;
        this.amount = amount;
        this.email = email;
        this.document = document;
        this.url = url;
    }
}
