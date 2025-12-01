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
    private readonly IRepositorio<PixTransaction> _repositorio;
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;
    private readonly WalletService _walletService;
    private readonly string _apiKey;

    private readonly ECDsa _ecdsa;

    public PixService(
        IRepositorio<PixTransaction> repositorio,
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

        // =======================================================
        // 1. Carregar chave privada EC (secp256k1)
        // =======================================================
        var privateKeyPath = _cfg["agilizepay:Cert:PrivateKeyPath"];


        if (!File.Exists(privateKeyPath))
            throw new Exception($"Chave privada não encontrada em: {privateKeyPath}");

        var privateKeyPem = File.ReadAllText(privateKeyPath);
        Console.WriteLine("🔍 [AGILIZEPAY] Lendo chave privada em: " + privateKeyPath);

        if (!File.Exists(privateKeyPath))
        {
            Console.WriteLine("❌ ERRO: Arquivo PEM não encontrado!");
            throw new Exception($"Chave privada não encontrada em: {privateKeyPath}");
        }

        _ecdsa = ECDsa.Create();
        // A curva secp256k1 é inferida pelo conteúdo do PEM.
        _ecdsa.ImportFromPem(privateKeyPem);

        // =======================================================
        // Headers fixos
        // =======================================================
        // O client_id é adicionado aqui e não será removido
        _http.DefaultRequestHeaders.Clear();
        _http.DefaultRequestHeaders.Add("client_id", _apiKey);
    }

    // =======================================================
    // Assinatura ECDSA secp256k1 -> SHA256 → HEX
    // =======================================================
    private string SignPayload(object payload)
    {
        // Opções para garantir JSON compactado (canônico para assinatura)
        // Não precisamos mais do PropertyNamingPolicy, pois o Dictionary garante as chaves
        var options = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false
        };

        var json = JsonSerializer.Serialize(payload, options);
        var bytes = Encoding.UTF8.GetBytes(json);

        // Uso do formato IeeeP1363FixedFieldConcatenation (.NET 8+)
        var signature = _ecdsa.SignData(
            bytes,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation
        );

        // Converte o array de bytes (R||S) para hexadecimal e minúsculas.
        return Convert.ToHexString(signature).ToLower();
    }
// .


    // =======================================================
    // PIX-IN
    // =======================================================
    public async Task<PixCharge?> CreatePixDepositAsync(PixDepositRequest req, User? user)
    {
        var amountString = req.Amount.ToString("F2", CultureInfo.InvariantCulture);

        // Ordem Alfabética (Lexicográfica) para canonicalização estrita:
        // amount, code, document, email, url
        var canonicalJson = "{" +
            $"\"amount\":\"{amountString}\"," +
            $"\"code\":\"{_cfg["agilizepay:CLIENT_ID"]}\"," +
            $"\"document\":\"{req.Document}\"," +
            $"\"email\":\"{user.Email}\"," +
            $"\"url\":\"{_cfg["agilizepay:PostbackUrl"]}\"" +
            "}";

        // Cria o objeto de payload (para o Content body)
        var payloadForBody = new Dictionary<string, object>
        {
            { "amount", amountString },
            { "code", _cfg["agilizepay:CLIENT_ID"] },
            { "document", req.Document },
            { "email", user.Email },
            { "url", _cfg["agilizepay:PostbackUrl"] }
        };
        
        Console.WriteLine("📦 [AGILIZEPAY] Payload enviado:");
        Console.WriteLine(canonicalJson); 
        
        var sign = SignPayload(canonicalJson);
        
        var url = _cfg["agilizepay:BaseUrl"];
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payloadForBody) 
        };
        
        request.Headers.Add("client_sign", sign);
        
        Console.WriteLine("📤 [AGILIZEPAY] Enviando headers:");
        Console.WriteLine($"  - client_id: {_apiKey}");
        Console.WriteLine($"  - client_sign: {sign}");
        Console.WriteLine($"{_cfg["agilizepay:BaseUrl"]} >> {canonicalJson}");

        var response = await _http.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"{nameof(CreatePixDepositAsync)} >> {content}");

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Erro AgilizePay PIX-IN: {content}");

        var pixResponse = System.Text.Json.JsonSerializer.Deserialize<PixResponse>(content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (pixResponse == null) return null;

        try
        {
            var insert = await _repositorio.InsertOneAsync(
                new PixTransaction
                {
                    UserId = user!.Id,
                    IdTransaction = pixResponse.txid,
                    Amount = req.Amount,
                    QrCode = pixResponse.qrCode,
                    QrCodeImageUrl = "",
                    PaidAt = null
                });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FALHA AO GRAVAR PIX] ID: {pixResponse.txid} | Erro: {ex.Message}");
        }

        return new PixCharge
        {
            Id = pixResponse.txid,
            QrCodeImageUrl = pixResponse.pixCopiaECola,
            BrCode = pixResponse.qrCode
        };
    }


    // =======================================================
    // PIX-OUT
    // =======================================================
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
    public async Task<bool> ProcessWebhookAsync(Transaction webhook)
    {
        if (webhook == null || string.IsNullOrWhiteSpace(webhook.IdTransaction))
            throw new ArgumentException("Webhook inválido.");

        var pixTx = await _repositorio.GetByIdTransactionAsync(webhook.IdTransaction);

        if (pixTx == null)
        {
            Console.WriteLine($"[WEBHOOK] Transação não encontrada: {webhook.IdTransaction}");
            return false;
        }

        if (pixTx.Status != "pending")
        {
            Console.WriteLine($"[WEBHOOK] Já processado: {webhook.IdTransaction}");
            return true;
        }

        bool success = false;

        if (webhook.TypeTransaction == "PAID_IN")
        {
            pixTx.Status = "Complete";
            pixTx.PaidAt = DateTime.UtcNow;
            await _repositorio.UpdateStatusAsync(webhook.IdTransaction, pixTx.Status, pixTx.PaidAt);

            try
            {
                await _walletService.CreditAsync(pixTx.UserId, pixTx.Amount, "PIX_IN");
                success = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WEBHOOK] Falha ao creditar: {ex.Message}");
            }
        }
        else if (webhook.StatusTransaction != "sucesso")
        {
            pixTx.Status = "Canceled";
            success = true;
        }
        else
        {
            return true;
        }

        return success;
    }

    public async Task CancelExpiredPixTransactionsAsync(int i)
    {
        throw new NotImplementedException();
    }
}