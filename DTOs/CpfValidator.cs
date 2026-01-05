using System.Text.RegularExpressions;

namespace DTOs;

public static class CpfValidator
{
    /// <summary>
    /// Valida se o CPF é válido
    /// </summary>
    public static bool IsValid(string cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return false;

        // Remove caracteres não numéricos
        cpf = Regex.Replace(cpf, @"[^\d]", "");

        // CPF deve ter exatamente 11 dígitos
        if (cpf.Length != 11)
            return false;

        // CPFs conhecidos como inválidos (todos os dígitos iguais)
        if (IsAllSameDigit(cpf))
            return false;

        // Valida os dois dígitos verificadores
        return ValidateDigits(cpf);
    }

    /// <summary>
    /// Formata CPF para exibição (000.000.000-00)
    /// </summary>
    public static string Format(string cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return string.Empty;

        cpf = Regex.Replace(cpf, @"[^\d]", "");

        if (cpf.Length != 11)
            return cpf;

        return $"{cpf.Substring(0, 3)}.{cpf.Substring(3, 3)}.{cpf.Substring(6, 3)}-{cpf.Substring(9, 2)}";
    }

    /// <summary>
    /// Remove formatação do CPF (deixa apenas números)
    /// </summary>
    public static string RemoveFormatting(string cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return string.Empty;

        return Regex.Replace(cpf, @"[^\d]", "");
    }

    private static bool IsAllSameDigit(string cpf)
    {
        return cpf == "00000000000" ||
               cpf == "11111111111" ||
               cpf == "22222222222" ||
               cpf == "33333333333" ||
               cpf == "44444444444" ||
               cpf == "55555555555" ||
               cpf == "66666666666" ||
               cpf == "77777777777" ||
               cpf == "88888888888" ||
               cpf == "99999999999";
    }

    private static bool ValidateDigits(string cpf)
    {
        // Primeiro dígito verificador
        int sum = 0;
        for (int i = 0; i < 9; i++)
        {
            sum += int.Parse(cpf[i].ToString()) * (10 - i);
        }

        int remainder = sum % 11;
        int firstDigit = remainder < 2 ? 0 : 11 - remainder;

        if (int.Parse(cpf[9].ToString()) != firstDigit)
            return false;

        // Segundo dígito verificador
        sum = 0;
        for (int i = 0; i < 10; i++)
        {
            sum += int.Parse(cpf[i].ToString()) * (11 - i);
        }

        remainder = sum % 11;
        int secondDigit = remainder < 2 ? 0 : 11 - remainder;

        return int.Parse(cpf[10].ToString()) == secondDigit;
    }
}