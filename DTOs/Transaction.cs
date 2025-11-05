namespace DTOs;

public class Transaction
{
    public string id { get; set; }
    public string status { get; set; }
    public decimal value { get; set; }

    public Transaction(string id, string status, decimal value)
    {
        this.id = id;
        this.status = status;
        this.value = value;
    }
}