namespace DTOs;

public class TokenRequest
{
    public string token { get; set; }
    public bool isInfluencer { get; set; }

    public TokenRequest(string token, bool isInfluencer)
    {
        this.token = token;
        this.isInfluencer = isInfluencer;
    }
}