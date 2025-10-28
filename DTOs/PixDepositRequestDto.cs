namespace DTOs;

public record PixDepositRequestDto(string token, decimal amount, string name, string email, string documentNumber, string phone);