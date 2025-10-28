namespace DTOs;

public record PixWithdrawRequestDto(string Token, decimal Amount, string PixKey, string PixKeyType);