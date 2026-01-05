using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DTOs;
using Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Services;

public class PaymentGateway : IPaymentGateway
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentGateway> _logger;
    private readonly string _accessToken;

    public PaymentGateway(HttpClient httpClient, IConfiguration configuration, ILogger<PaymentGateway> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _accessToken = _configuration["MercadoPago:AccessToken"];
    }

    public async Task<PaymentResponse> CreatePaymentAsync(Order order, PixDepositRequest _responsavel, string paymentMethod)
    {
        try
        {
            _logger.LogInformation("🚀 === INICIANDO PAGAMENTO (MODO USUÁRIO VINCULADO) ===");
            
            // Validação do CPF
            var cleanCpf = _responsavel.Document?.Replace(".", "").Replace("-", "").Trim();
            if (string.IsNullOrEmpty(cleanCpf) || cleanCpf.Length != 11)
            {
                _logger.LogError("❌ CPF inválido: {CPF}", _responsavel.Document);
                return new PaymentResponse 
                { 
                    Success = false, 
                    Message = "CPF inválido. Deve conter 11 dígitos." 
                };
            }

            var _User = new
            {
                Id = order.id,
                Email = _responsavel.Email,
                FirstName = _responsavel.SplitEmail,
                LastName = _responsavel.SplitEmail,
            };
            
            if (_User == null)
            {
                _logger.LogWarning("⚠️ Falha na criação vinculada. Usando estratégia Guest pura.");
                return await CreatePaymentAsGuestAsync(order, cleanCpf, paymentMethod);
            }

            _logger.LogInformation("👤 User Criado: {Email} | ID: {Id}", _User.Email, _User.Id);

            // Validação do método de pagamento
            if (string.IsNullOrEmpty(paymentMethod))
            {
                _logger.LogError("❌ Método de pagamento não informado");
                return new PaymentResponse 
                { 
                    Success = false, 
                    Message = "Método de pagamento não informado" 
                };
            }

            var requestUrl = "https://api.mercadopago.com/v1/payments";

            string mpPaymentMethodId = paymentMethod.ToLower() switch
            {
                "pix" => "pix",
                "boleto" => "bolbradesco",
                "credit_card" => "credit_card",
                _ => throw new ArgumentException($"Método de pagamento '{paymentMethod}' não suportado. Use: pix, boleto ou credit_card")
            };

            // MONTA PAYLOAD USANDO O USUÁRIO VINCULADO
            var payload = new
            {
                transaction_amount = (double)order.TotalAmount,
                description = $"Pedido Petrakka #{order.id.Substring(0, Math.Min(8, order.id.Length))}",
                payment_method_id = mpPaymentMethodId,
                payer = new
                {
                    email = _User.Email ?? $"cliente_{cleanCpf}@petrakka.com",
                    first_name = _User.FirstName ?? "Cliente",
                    last_name = _User.LastName ?? "Petrakka",
                    identification = new
                    {
                        type = "CPF",
                        number = cleanCpf
                    },
                    // Adiciona endereço para boleto (obrigatório)
                    address = paymentMethod.ToLower() == "boleto" ? GetDefaultAddress() : null
                },
                external_reference = order.id,
                binary_mode = true,
                notification_url = "https://petrakka.com:7231/api/Caixa/webhook"
            };

            var result = await SendPaymentRequestAsync(requestUrl, payload, paymentMethod);
            
            // TRATAMENTO ESPECÍFICO PARA ERRO DE PIX NÃO HABILITADO
            if (!result.Success && 
                (result.Message.Contains("key enabled") || 
                 result.Message.Contains("Financial Identity") ||
                 result.Message.Contains("QR render")))
            {
                _logger.LogWarning("⚠️ PIX não disponível na conta MercadoPago");
                
                // Se era PIX, retorna erro explicativo
                if (paymentMethod.ToLower() == "pix")
                {
                    return new PaymentResponse
                    {
                        Success = false,
                        Message = "❌ PIX NÃO HABILITADO: Para usar PIX, você precisa:\n" +
                                  "1. Acessar o painel do MercadoPago (https://www.mercadopago.com.br/developers/panel)\n" +
                                  "2. Ir em 'Suas integrações' → Selecionar sua aplicação\n" +
                                  "3. Verificar e ativar o PIX\n" +
                                  "4. Completar o cadastro da conta\n\n" +
                                  "💡 Alternativa: Use BOLETO como método de pagamento.",
                        Details = new PaymentDetails 
                        { 
                            PaymentMethod = "pix_erro_configuracao"
                        }
                    };
                }
            }
            
            return result;
        }
        catch (ArgumentException argEx)
        {
            _logger.LogError(argEx, "❌ Erro de validação");
            return new PaymentResponse { Success = false, Message = argEx.Message };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "🔥 Exception no MercadoPagoService");
            return new PaymentResponse { Success = false, Message = "Erro interno: " + ex.Message };
        }
    }

    public async Task<PaymentResponse> GetPaymentAsync(string transactionId)
    {
        try
        {
            var requestUrl = $"https://api.mercadopago.com/v1/payments/{transactionId}";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new PaymentResponse { Success = false, Message = "Pagamento não encontrado." };

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;

            var result = new PaymentResponse
            {
                Success = true,
                TransactionId = transactionId,
                Message = root.GetProperty("status").GetString() ?? "pending",
                Details = new PaymentDetails()
            };

            // Tenta pegar o valor com segurança
            if(root.TryGetProperty("transaction_amount", out var amountProp))
                result.Amount = amountProp.GetDecimal();

            string type = root.GetProperty("payment_method_id").GetString()!;
            result.Details.PaymentMethod = type;

            if (type == "pix")
            {
                if (root.TryGetProperty("point_of_interaction", out var poi) &&
                    poi.TryGetProperty("transaction_data", out var tData))
                {
                    result.Details.PixQrCode = tData.GetProperty("qr_code").GetString();
                    result.Details.PixQrCodeImage = tData.GetProperty("qr_code_base64").GetString();
                    result.Details.PixExpirationDate = DateTime.Now.AddMinutes(30); 
                }
            }
            else if (type == "bolbradesco" || type == "boleto")
            {
                if (root.TryGetProperty("transaction_details", out var tDetails) &&
                    tDetails.TryGetProperty("external_resource_url", out var pdfUrl))
                {
                    result.Details.BoletoPdfUrl = pdfUrl.GetString();
                }
                if (root.TryGetProperty("barcode", out var barcode) && 
                    barcode.TryGetProperty("content", out var barContent))
                {
                    result.Details.BoletoBarCode = barContent.GetString();
                }
                if (root.TryGetProperty("date_of_expiration", out var expiration))
                {
                    // Tratamento de data seguro
                    if (DateTime.TryParse(expiration.GetString(), out var date))
                        result.Details.BoletoDueDate = date;
                    else
                        result.Details.BoletoDueDate = DateTime.Now.AddDays(3);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar pagamento");
            return new PaymentResponse { Success = false, Message = "Erro ao recuperar dados." };
        }
    }
    
    private object GetDefaultAddress()
    {
        return new 
        {
            zip_code = "01310-100",
            street_name = "Av. Paulista",
            street_number = "1000",
            neighborhood = "Bela Vista",
            city = "São Paulo",
            federal_unit = "SP"
        };
    }

    // Estratégia de Fallback (Guest puro com domínio .com genérico)
    private async Task<PaymentResponse> CreatePaymentAsGuestAsync(Order order, string cleanCpf, string paymentMethod)
    {
        _logger.LogInformation("🔄 Usando modo Guest para pagamento");
        
        var requestUrl = "https://api.mercadopago.com/v1/payments";
        string mpPaymentMethodId = paymentMethod.ToLower() == "pix" ? "pix" : "bolbradesco";
        
        string guestEmail = $"guest_{Guid.NewGuid().ToString().Substring(0,6)}@sandbox-pagamento.net";

        var payload = new
        {
            transaction_amount = (double)order.TotalAmount,
            description = $"Pedido Petrakka #{order.id.Substring(0, Math.Min(8, order.id.Length))}",
            payment_method_id = mpPaymentMethodId,
            payer = new
            {
                email = guestEmail,
                first_name = "Comprador",
                last_name = "Guest",
                identification = new
                {
                    type = "CPF",
                    number = cleanCpf
                },
                // Endereço obrigatório para Boleto
                address = GetDefaultAddress() 
            },
            external_reference = order.id,
            binary_mode = true,
            notification_url = "https://petrakka.com:7231/api/Caixa/webhook"
        };

        return await SendPaymentRequestAsync(requestUrl, payload, paymentMethod);
    }

    private async Task<PaymentResponse> SendPaymentRequestAsync(string url, object payload, string paymentMethod)
    {
        var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions 
        { 
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        
        _logger.LogDebug("📦 Payload ({Method}): {Json}", paymentMethod, jsonPayload);

        var requestContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Headers.Add("X-Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = requestContent;

        var response = await _httpClient.SendAsync(request);
        var responseString = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("❌ Erro MP [Status {Status}]: {Response}", response.StatusCode, responseString);
            string errorMsg = "Erro no gateway de pagamento.";
            
            try 
            {
                using var errDoc = JsonDocument.Parse(responseString);
                var root = errDoc.RootElement;
                
                // Captura mensagem de erro
                if (root.TryGetProperty("message", out var msg)) 
                    errorMsg = msg.GetString() ?? errorMsg;
                
                // Captura descrição detalhada do erro
                if (root.TryGetProperty("cause", out var causes) && causes.GetArrayLength() > 0)
                {
                    var firstCause = causes[0];
                    if (firstCause.TryGetProperty("description", out var desc))
                        errorMsg += $" - {desc.GetString()}";
                    
                    if (firstCause.TryGetProperty("code", out var code))
                        _logger.LogError("Código do erro: {Code}", code.GetInt32());
                }
            } 
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Não foi possível parsear erro do MercadoPago");
            }
            
            return new PaymentResponse { Success = false, Message = errorMsg };
        }

        _logger.LogInformation("✅ Pagamento criado com sucesso");
        
        using var doc = JsonDocument.Parse(responseString);
        var _root = doc.RootElement;
        
        var result = new PaymentResponse
        {
            Success = true,
            TransactionId = _root.GetProperty("id").ToString(),
            Message = "Pagamento gerado com sucesso",
            Details = new PaymentDetails { PaymentMethod = paymentMethod }
        };

        if (paymentMethod.ToLower() == "pix")
        {
            if (_root.TryGetProperty("point_of_interaction", out var poi) &&
                poi.TryGetProperty("transaction_data", out var tData))
            {
                result.Details.PixQrCode = tData.GetProperty("qr_code").GetString();
                result.Details.PixQrCodeImage = tData.GetProperty("qr_code_base64").GetString();
                result.Details.PixExpirationDate = DateTime.Now.AddMinutes(30);
                
                _logger.LogInformation("✅ QR Code PIX gerado com sucesso");
            }
            else
            {
                _logger.LogWarning("⚠️ PIX criado mas QR Code não encontrado na resposta");
            }
        }
        else if (paymentMethod.ToLower() == "boleto")
        {
            if (_root.TryGetProperty("transaction_details", out var tDetails) &&
                tDetails.TryGetProperty("external_resource_url", out var pdfUrl))
            {
                result.Details.BoletoPdfUrl = pdfUrl.GetString();
            }
            
            if (_root.TryGetProperty("barcode", out var barcode) && 
                barcode.TryGetProperty("content", out var barContent))
            {
                result.Details.BoletoBarCode = barContent.GetString();
            }
            
            // Tentativa segura de data
            if (_root.TryGetProperty("date_of_expiration", out var expiration))
            {
                if (DateTime.TryParse(expiration.GetString(), out var date))
                    result.Details.BoletoDueDate = date;
            }
            
            if (!result.Details.BoletoDueDate.HasValue) 
                result.Details.BoletoDueDate = DateTime.Now.AddDays(3);
            
            _logger.LogInformation("✅ Boleto gerado com sucesso");
        }

        return result;
    }

    private class TestUserDto 
    { 
        public string Id { get; set; } = ""; 
        public string Email { get; set; } = ""; 
    }

    private async Task<TestUserDto?> CreateTestUserWithCpfAsync(string cpf)
    {
        try
        {
            var url = "https://api.mercadopago.com/users/test_user";
            var payload = new 
            { 
                site_id = "MLB",
                description = "Comprador de Testes"
            };
            
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                return new TestUserDto 
                { 
                    Id = doc.RootElement.GetProperty("id").ToString(), 
                    Email = doc.RootElement.GetProperty("email").GetString()!
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar usuário de teste");
        }
        return null;
    }
}