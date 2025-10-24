namespace DTOs;

public record PixWebhookDto(string idTransaction, string status, string typeTransaction, decimal amount, long userId);
