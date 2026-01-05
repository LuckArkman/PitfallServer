using System.ComponentModel.DataAnnotations;

namespace DTOs;

public class CheckoutWithCpfRequest
{
    [Required(ErrorMessage = "O método de pagamento é obrigatório")]
    [RegularExpression("^(card|pix|boleto)$", ErrorMessage = "Método de pagamento inválido")]
    public string PaymentMethod { get; set; } = string.Empty;

    public string consultaId { get; set; }

    // CPF obrigatório para PIX e Boleto
    [RequiredIfPaymentMethod("pix", "boleto", ErrorMessage = "CPF é obrigatório para PIX e Boleto")]
    [CpfValidation(ErrorMessage = "CPF inválido")]
    public string? Cpf { get; set; }

    // Dados de cartão (opcional - apenas se PaymentMethod == "card")
    public string? CardName { get; set; }
    public string? CardNumber { get; set; }
    public string? CardExpiry { get; set; }
    public string? CardCvv { get; set; }
}
