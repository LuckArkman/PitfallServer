namespace DTOs;

public record PixDepositRequest(decimal Amount, string Name, string Email, string Document, string Phone, string? SplitEmail = null, decimal SplitPercentage = 0);
