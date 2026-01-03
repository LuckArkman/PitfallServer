namespace DTOs;

public record PixWithdrawRequestDto(string Token,string cpf, decimal Amount, string PixKey, string PixKeyType);