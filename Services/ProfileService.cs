using DTOs;
using Interfaces;
using Microsoft.Extensions.Configuration;

namespace Services;

public class ProfileService
{
    private readonly IRepositorio<User> _repositorio;
    private readonly WalletService _walletService;
    private readonly WalletLedgerService _ledgerService;
    private readonly IConfiguration _cfg;
    readonly HttpClient _http;

    public ProfileService(
        IRepositorio<User> repositorio,
        WalletService walletService,
        WalletLedgerService ledgerService,
        HttpClient http,
        IConfiguration cfg)
    {
        _repositorio = repositorio;
        _walletService = walletService;
        _ledgerService = ledgerService;
        _http = http;
        _cfg = cfg;
        _walletService = walletService;
        _repositorio.InitializeCollection(_cfg["MongoDbSettings:ConnectionString"],
            _cfg["MongoDbSettings:DataBaseName"],
            "WalletLedger");
    }

    /// <summary>
    /// Retorna o perfil completo do usuário: dados pessoais, carteira e histórico de transações.
    /// </summary>
    public async Task<object?> GetProfile(Guid userId)
    {
        var user = await _repositorio.GetByIdAsync(
            id: userId,
            none: CancellationToken.None);
        var wallet = await _walletService.GetOrCreateWalletAsync(userId);
        var ledgerList = await _ledgerService.GetAllLedger(wallet.Id);

        // 🔹 4. Retornar tudo junto
        return new
        {
            User = user,
            Wallet = wallet,
            Transactions = ledgerList
        };
    }
}
