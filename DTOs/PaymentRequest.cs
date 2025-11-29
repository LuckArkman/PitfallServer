namespace DTOs;

public class PaymentRequest
{
    public string token { get; set; }
    public string secret { get; set; }
    public string postback { get; set; }
    public decimal amount { get; set; }
    public string debtor_name { get; set; }
    public string email { get; set; }
    public string debtor_document_number { get; set; }
    public string phone { get; set; }
    public string method_pay { get; set; }
    public string split_email { get; set; }
    public decimal split_percentage { get; set; }
}
