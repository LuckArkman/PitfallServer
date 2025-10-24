namespace DTOs;

public record PixWithdrawRequest(decimal Amount, string PixKey, string PixKeyType);