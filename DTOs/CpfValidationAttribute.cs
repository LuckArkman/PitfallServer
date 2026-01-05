using System.ComponentModel.DataAnnotations;

namespace DTOs;

public class CpfValidationAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            return ValidationResult.Success; // Validação de obrigatoriedade é feita por outro atributo

        var cpf = value.ToString()!;
        
        if (!CpfValidator.IsValid(cpf))
        {
            return new ValidationResult(ErrorMessage ?? "CPF inválido");
        }

        return ValidationResult.Success;
    }
}