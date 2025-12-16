namespace DTOs;

public record PixWithdrawResponse(string Id, decimal Amount, string PixKey, string PixKeyType, string WithdrawStatusId);
