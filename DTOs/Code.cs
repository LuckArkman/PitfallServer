namespace DTOs;

public class Code
{
    public int Id { get; set; }

    public int AfiliadoId { get; set; }

    public string Af_key { get; set; } = string.Empty;

    public int Af_level { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public DateTime DataRegistro { get; set; }
}
