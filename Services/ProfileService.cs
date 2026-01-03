using DTOs;
using Interfaces;
using Microsoft.Extensions.Configuration;

namespace Services;

public class ProfileService
{
    private readonly IUserRepositorio<User> _repositorio;
    private readonly IWalletRepositorio<Wallet> _walletService;
    private readonly IWalletLedgerRepositorio<WalletLedger> _ledgerService;
    readonly UserRankingService _userRankingService;
    private readonly IConfiguration _cfg;
    readonly HttpClient _http;

    public ProfileService(
        IUserRepositorio<User> repositorio,
        IWalletRepositorio<Wallet> walletService,
        IWalletLedgerRepositorio<WalletLedger> ledgerService,
        HttpClient http,
        UserRankingService userRankingService,
        IConfiguration cfg)
    {
        _repositorio = repositorio;
        _walletService = walletService;
        _ledgerService = ledgerService;
        _http = http;
        _userRankingService = userRankingService;
        _cfg = cfg;
        _walletService = walletService;
        _ledgerService.InitializeCollection(_cfg["MongoDbSettings:ConnectionString"],
            _cfg["MongoDbSettings:DataBaseName"],
            "WalletLedger");
        _repositorio.InitializeCollection(_cfg["MongoDbSettings:ConnectionString"],
            _cfg["MongoDbSettings:DataBaseName"],
            "Users");
        _walletService.InitializeCollection(_cfg["MongoDbSettings:ConnectionString"],
            _cfg["MongoDbSettings:DataBaseName"],
            "Wallets");
    }

    /// <summary>
    /// Retorna o perfil completo do usuário: dados pessoais, carteira e histórico de transações.
    /// </summary>
    public async Task<object?> GetProfile(Guid userId)
    {
        var user = await _repositorio.GetByIdAsync(
            id: userId,
            none: CancellationToken.None);
        var wallet = await _walletService.GetWalletByUserIdAsync(userId, CancellationToken.None);
        var ledgerList = await _ledgerService.GetAllWalletLedger(wallet.Id, CancellationToken.None);
        var rank = await _userRankingService.GetRanking(userId);

        // 🔹 4. Retornar tudo junto
        return new
        {
            User = user,
            Wallet = wallet,
            ranking = rank,
            Transactions = ledgerList
        };
    }

    public async Task<object> GetInvite_Profile(Guid userId)
{
    // 1. Recuperação do usuário inicial & 2. Identificação do código de indicação
    var requester = await _repositorio.GetByIdAsync(userId, CancellationToken.None);
    var rank = await _userRankingService.GetRanking(userId);
    var requesterWallet = await _walletService.GetWalletByUserIdAsync(userId, CancellationToken.None);
    
    if (requester == null)
        throw new InvalidOperationException("Usuário não encontrado");

    // 3. Recuperação dos usuários indicados
    // Busca usuários que usaram o ReferralCode do requester no campo registerCode
    var invitedUsers = await _repositorio.GetUsersByRegisterCode(requester.ReferralCode);

    if (invitedUsers == null || !invitedUsers.Any())
    {
        return new {
            user = requester,
            ranking = rank,
            wallet = requesterWallet,
            invites = new List<object>() 
        };
    }

    var resultList = new List<object>();

    // Processamento individual para cada indicado (Etapas 4 a 8)
    foreach (var invitedUser in invitedUsers)
    {
        // 4. Recuperação do ranking dos usuários indicados
        var userRanking = await _userRankingService.GetRanking(invitedUser.Id);

        // 5. Recuperação das carteiras dos usuários indicados
        var userWallet = await _walletService.GetWalletByUserIdAsync(invitedUser.Id, CancellationToken.None);

        decimal totalDeposits = 0;
        DateTime? lastDepositDate = null;

        if (userWallet != null)
        {
            // 6. Recuperação dos Ledgers (Leaders) das carteiras
            var userLedger = await _ledgerService.GetAllWalletLedger(userWallet.Id, CancellationToken.None);

            if (userLedger != null)
            {
                // 7. Filtragem das recargas válidas (Status/Type == "PIX_IN")
                var validDeposits = userLedger
                    .Where(l => l != null && l.Type == "PIX_IN")
                    .ToList();

                totalDeposits = validDeposits.Sum(l => l.Amount);
                lastDepositDate = validDeposits.OrderByDescending(l => l.CreatedAt).FirstOrDefault()?.CreatedAt;
            }
        }

        // 8. Consolidação dos dados
        resultList.Add(new
        {
            UserId = invitedUser.Id,
            Email = invitedUser.Email,
            Name = invitedUser.Name,
            RegisteredAt = invitedUser.CreatedAt,
            Ranking = new
            {
                Level = userRanking?._level ?? 0,
                Points = userRanking?._points ?? 0,
                Registers = userRanking?._registers ?? 0,
                PointsNextLevel = userRanking?._pointsNextLevel ?? 0
            },
            TotalDeposits = totalDeposits,
            LastDepositDate = lastDepositDate
        });
    }

    // 9. Geração da lista final (Ordenada por total de depósitos)
    return new
    {
        user = requester,
        ranking = rank,
        wallet = requesterWallet,
        invites = resultList.OrderByDescending(x => ((dynamic)x).TotalDeposits).ToList()
    };
}
}
     
