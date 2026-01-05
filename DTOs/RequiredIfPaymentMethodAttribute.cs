using System.ComponentModel.DataAnnotations;

namespace DTOs;

public class RequiredIfPaymentMethodAttribute : ValidationAttribute
{
    private readonly string[] _requiredMethods;

    public RequiredIfPaymentMethodAttribute(params string[] requiredMethods)
    {
        _requiredMethods = requiredMethods;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var instance = validationContext.ObjectInstance;
        var paymentMethodProperty = instance.GetType().GetProperty("PaymentMethod");
        
        if (paymentMethodProperty == null)
            return ValidationResult.Success;

        var paymentMethod = paymentMethodProperty.GetValue(instance)?.ToString()?.ToLower();
        
        if (paymentMethod != null && _requiredMethods.Contains(paymentMethod))
        {
            if (string.IsNullOrWhiteSpace(value?.ToString()))
            {
                return new ValidationResult(ErrorMessage ?? "Campo obrigatório para este método de pagamento");
            }
        }

        return ValidationResult.Success;
    }
}