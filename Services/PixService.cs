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
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;
    private readonly WalletService _walletService;
    private readonly string _apiKey;

    private readonly ECDsa _ecdsa;

    public PixService(
        IPixTransactionRepositorio<PixTransaction> repositorio,
        HttpClient http,
        IConfiguration cfg,
        WalletService walletService)
    {
        _repositorio = repositorio;
        _http = http;
        _cfg = cfg;
        _walletService = walletService;
        _repositorio.InitializeCollection(_cfg["MongoDbSettings:ConnectionString"],
            _cfg["MongoDbSettings:DataBaseName"],
            "Transactions");
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
        var json = JsonSerializer.Serialize(request);

        using var content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        using var _response = await _http.PostAsync(
            _cfg["kortexpay:BaseUrl"],
            content,
            CancellationToken.None);

        if (!_response.IsSuccessStatusCode)
        {
            var error = await _response.Content.ReadAsStringAsync(CancellationToken.None);
            throw new HttpRequestException(
                $"Erro ao chamar API de pagamento: {_response.StatusCode} - {error}");
        }

        var responseJson = await _response.Content.ReadAsStringAsync(CancellationToken.None);
        Console.WriteLine(responseJson);
        var response = JsonSerializer.Deserialize<PixChargeResponse>(
            responseJson,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        var insert = await _repositorio.InsertOneAsync(new PixTransaction
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            IdTransaction = response.IdTransaction,
            Amount = req.Amount,
            QrCode = response.QrCode,
            QrCodeImageUrl = response.QrCodeImageUrl,
        });
        return new PixCharge
        {
            Id = response.Charge.Id,
            QrCodeImageUrl = response.Charge.QrCode,
            BrCode = response.Charge.BrCode
        };
    }
    public async Task<PixWithdrawResponse?> CreatePixWithdrawAsync(PixWithdrawRequest req, User? user)
    {
        var payload = new
        {
            nome = user.Name ?? "Conta Origem",
            cpf = "00000000000",
            valor = req.Amount.ToString("F2"),
            descricao = "Saque de fundos",
            postback = _cfg["agilizepay:WithdrawCallbackUrl"]
        };

        // Assinar request
        var sign = SignPayload(payload);
        _http.DefaultRequestHeaders.Remove("client_sign");
        _http.DefaultRequestHeaders.Add("client_sign", sign);

        var url = _cfg["agilizepay:BaseUrl"]!.TrimEnd('/') + "/api/v1/cashout";

        var res = await _http.PostAsJsonAsync(url, payload);

        if (!res.IsSuccessStatusCode)
            throw new Exception($"Erro AgilizePay PIX-OUT: {await res.Content.ReadAsStringAsync()}");

        return await res.Content.ReadFromJsonAsync<PixWithdrawResponse>();
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