namespace DTOs;

public record PixDepositRequestDto(string Token, decimal Amount, string Name, string Email, string Document, string Phone, string? SplitEmail, decimal SplitPercentage);