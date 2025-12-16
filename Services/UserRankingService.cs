using DTOs;
using Interfaces;
using Microsoft.Extensions.Configuration;

namespace Services;

public class UserRankingService
{
    private readonly IRepositorio<UserRanking> _repositorio;
    private readonly IConfiguration _cfg;
    public UserRankingService(
        IRepositorio<UserRanking> repositorio,
        IConfiguration cfg)
    {
        _repositorio = repositorio;
        _cfg = cfg;
        _repositorio.InitializeCollection(_cfg["MongoDbSettings:ConnectionString"],
            _cfg["MongoDbSettings:DataBaseName"],
            "UserRanking");
    }

    public async Task<UserRanking?> InsertRanking(User user)
    {
        var rk = await _repositorio.InsertOneAsync(
            new UserRanking
            {
                _userId =  user.Id,
            });
        return rk;
    }

    public async Task UpdateRanking(User? user)
    {
        var rk = await _repositorio.GetUserIdAsync(user!.Id, CancellationToken.None);
        if(rk is not null) rk.OnLevelUp();
        await _repositorio.UpdateRanking(rk, CancellationToken.None);
        
    }
}