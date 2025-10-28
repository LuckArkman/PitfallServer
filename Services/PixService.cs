using System.Net.Http.Json;
using DTOs;
using Microsoft.Extensions.Configuration;

namespace Services;

public class PixService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;

    public PixService(HttpClient http, IConfiguration cfg)
    {
        _http = http;
        _cfg = cfg;
    }

    // ===================== PIX IN ======================
    public async Task<PixDepositResponse?> CreatePixDepositAsync(PixDepositRequest req)
    {
        var payload = new
        {
            token = _cfg["FeiPay:Token"],
            secret = _cfg["FeiPay:Secret"],
            postback = _cfg["FeiPay:PostbackUrl"],
            amount = req.Amount,
            debtor_name = req.Name,
            email = req.Email,
            debtor_document_number = req.Document,
            phone = req.Phone,
            method_pay = "pix",
            split_email = "",
            split_percentage = ""
        };

        var res = await _http.PostAsJsonAsync("wallet/deposit/payment", payload);
        if (!res.IsSuccessStatusCode)
        {
            throw new Exception($"Erro FeiPay PIX-IN: {await res.Content.ReadAsStringAsync()}");
        }

        return await res.Content.ReadFromJsonAsync<PixDepositResponse>();
    }

    // ===================== PIX OUT ======================
    public async Task<PixWithdrawResponse?> CreatePixWithdrawAsync(PixWithdrawRequest req)
    {
        var payload = new
        {
            token = _cfg["FeiPay:Token"],
            secret = _cfg["FeiPay:Secret"],
            baasPostbackUrl = _cfg["FeiPay:WithdrawCallbackUrl"],
            amount = req.Amount,
            pixKey = req.PixKey,
            pixKeyType = req.PixKeyType
        };

        var res = await _http.PostAsJsonAsync("pixout", payload);
        if (!res.IsSuccessStatusCode)
        {
            throw new Exception($"Erro FeiPay PIX-OUT: {await res.Content.ReadAsStringAsync()}");
        }

        return await res.Content.ReadFromJsonAsync<PixWithdrawResponse>();
    }
}