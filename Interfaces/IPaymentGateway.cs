using DTOs;

namespace Interfaces;

public interface IPaymentGateway
{
    Task<PaymentResponse> CreatePaymentAsync(Order order, PixDepositRequest _responsavel, string paymentMethod);
    Task<PaymentResponse?> GetPaymentAsync(string transactionId);
}