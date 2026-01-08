using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Data;
using DTOs;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Security.Cryptography;
using System.Globalization;
using Interfaces;

namespace Services;

public class PixService
{
    private readonly IPixTransactionRepositorio<PixTransaction> _repositorio;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IRequestTransactionRepositorio<RequestTransaction> _repositorioRequestTransaction;
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;
    private readonly WalletService _walletService;
    private readonly string _apiKey;

    private readonly ECDsa _ecdsa;

    public PixService(
        IPixTransactionRepositorio<PixTransaction> repositorio,
        IPaymentGateway paymentGateway,
        IRequestTransactionRepositorio<RequestTransaction> repositorioRequestTransaction,
        HttpClient http,
        IConfiguration cfg,
        WalletService walletService)
    {
        _repositorio = repositorio;
        _paymentGateway = paymentGateway;
        _repositorioRequestTransaction = repositorioRequestTransaction;
        _http = http;
        _cfg = cfg;
        _walletService = walletService;
        _repositorio.InitializeCollection(_cfg["MongoDbSettings:ConnectionString"],
            _cfg["MongoDbSettings:DataBaseName"],
            "Transactions");
        _repositorioRequestTransaction.InitializeCollection(_cfg["MongoDbSettings:ConnectionString"],
            _cfg["MongoDbSettings:DataBaseName"],
            "RequestTransactions");
    }

    // =======================================================
    // Assinatura ECDSA secp256k1 -> SHA256 → HEX
    // =======================================================
    private string SignPayload(object payload)
    {
        var options = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false
        };

        var json = JsonSerializer.Serialize(payload, options);
        var bytes = Encoding.UTF8.GetBytes(json);
        var signature = _ecdsa.SignData(
            bytes,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation
        );
        return Convert.ToHexString(signature).ToLower();
    }
// .


    // =======================================================
    // PIX-IN
    // =======================================================
    public async Task<PixCharge?> CreatePixDepositAsync(PixDepositRequest req, User? user)
    {
        var amountString = req.Amount.ToString("F2", CultureInfo.InvariantCulture);

        var request = new PaymentRequestDto
        {
            Token = _cfg["kortexpay:token"],
            Secret = _cfg["kortexpay:secret_Key"],
            Postback = _cfg["kortexpay:PostbackUrl"],
            Amount = req.Amount,
            DebtorName = user.Name,
            DebtorDocumentNumber = req.Document,
            Email = user.Email,
            MethodPay = "pix",
            Phone = "00 90000-0000",
            SplitEmail = user.Email,
            SplitPercentage = 0,
            
            
        };
        var order = new Order
        {
            id = Guid.NewGuid().ToString(),
            UserId = user.Id.ToString(),
            TotalAmount = req.Amount,
            PaymentMethod = "pix",
            Status = "Pending",
        };
        var _pixDepositRequest = new PixDepositRequest(
            req.Amount,
            user.Name,
            user.Email,
            req.Document,
            "00 90000-0000",
            user.Email,
            0
        );
        var _req = await _paymentGateway.CreatePaymentAsync(order, _pixDepositRequest, "pix");
        if (_req != null)
        {
            var insert = await _repositorio.InsertOneAsync(new PixTransaction
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                IdTransaction = _req.TransactionId,
                Amount = req.Amount,
                QrCode = _req.Details.PixQrCode,
                QrCodeImageUrl = _req.Details.PixQrCodeImage,
            });
            return new PixCharge
            {
                Id = _req.TransactionId,
                QrCodeImageUrl = _req.Details.PixQrCodeImage,
                BrCode = _req.Details.PixQrCode,
            };
        }

        return null;
    }
    public async Task<PixWithdrawResponse?> CreatePixWithdrawAsync(PixWithdrawRequest? req, User? user)
    {
        var wallet = await _walletService.GetOrCreateWalletAsync(user.Id);
        var request = new RequestTransaction
        {
            userId = user.Id,
            walletId = wallet.Id,
            name = user.Name,
            email = user.Email,
            document = req.cpf,
            amount  = req.Amount,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
        };
        var _rt = await _repositorioRequestTransaction.InsertOneAsync(request);
        if (_rt == null) return null;
        return new PixWithdrawResponse(_rt.id, _rt.amount, _rt.Status);
    }


    // =======================================================
    // Webhook + Rotinas existentes (sem alteração)
    // =======================================================
    public async Task<bool> ProcessWebhookAsync(PaymentStatusDto webhook)
    {
        if (webhook == null || string.IsNullOrWhiteSpace(webhook.IdTransaction))
            throw new ArgumentException("Webhook inválido.");

        var pixTx = await _repositorio.GetByIdTransactionAsync(webhook.IdTransaction);

        if (pixTx == null)
        {
            Console.WriteLine($"[WEBHOOK] Transação não encontrada: {webhook.IdTransaction}");
            return false;
        }

        if (webhook.TypeTransaction == "RECEIVEPIX")
        {
            pixTx.Status = "Complete";
            pixTx.PaidAt = DateTime.UtcNow;
            await _repositorio.UpdateStatusAsync(webhook.IdTransaction, pixTx.Status, pixTx.PaidAt);

            try
            {
                await _walletService.CreditAsync(pixTx.UserId, pixTx.Amount, "PIX_IN");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WEBHOOK] Falha ao creditar: {ex.Message}");
            }
        }
        else if (webhook.Status != "paid")
        {
            pixTx.Status = "Canceled";
            return true;
        }
        else
        {
            return true;
        }

        return false;
    }

    public async Task CancelExpiredPixTransactionsAsync(int i)
    {
        throw new NotImplementedException();
    }
}