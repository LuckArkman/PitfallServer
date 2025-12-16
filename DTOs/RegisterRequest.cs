namespace DTOs;

public class RegisterRequest
{
    public string Email { get; set; }
    public string Password { get; set; }

    // opcional: enviado pelo front
    public string? RefCode { get; set; }
}