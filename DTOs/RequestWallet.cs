namespace DTOs;

public class RequestWallet
{
    public string token { get; set; }

    public RequestWallet(string token)
    {
        this.token = token;
    }
}