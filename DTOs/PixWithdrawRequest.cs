namespace DTOs;

public record PixWithdrawRequest(decimal Amount, string cpf, string PixKey, string PixKeyType);