using Data;
using DTOs;
using Npgsql;
using System.Text.Json;

namespace Core;

public class CommissionService
{
    private readonly AppDbContext _db;

    private const decimal PCT_L1 = 0.05m;
    private const decimal PCT_L2 = 0.03m;
    private const decimal PCT_L3 = 0.02m;

    public CommissionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task GenerateAffiliateCommissionsAsync(RoundDto round)
    {
        if (round == null) throw new ArgumentNullException(nameof(round));
        if (!string.Equals(round.Result, "lose", StringComparison.OrdinalIgnoreCase))
            return;

        var lossBase = round.BetAmount - (round.PrizeTotal ?? 0m);
        if (lossBase <= 0m) return;

        // Carregar informações do usuário e uplines
        var user = await LoadUserAsync(round.UserId);
        if (user == null) return;

        Guid? l1 = user.InviterL1;
        Guid? l2 = user.InviterL2;
        Guid? l3 = user.InviterL3;

        if (l1 == null && l2 == null && l3 == null)
            return;

        await _db.ExecuteTransactionAsync(async tx =>
        {
            if (l1 != null)
            {
                await CreatePayoutAsync(tx,
                    receiverUserId: l1.Value,
                    sourceUserId: round.UserId,
                    level: 1,
                    amount: Math.Round(lossBase * PCT_L1, 8),
                    basisAmount: lossBase,
                    roundId: round.Id,
                    ledgerType: "affiliate_lv1",
                    percent: 5
                );
            }

            if (l2 != null)
            {
                await CreatePayoutAsync(tx,
                    receiverUserId: l2.Value,
                    sourceUserId: round.UserId,
                    level: 2,
                    amount: Math.Round(lossBase * PCT_L2, 8),
                    basisAmount: lossBase,
                    roundId: round.Id,
                    ledgerType: "affiliate_lv2",
                    percent: 3
                );
            }

            if (l3 != null)
            {
                await CreatePayoutAsync(tx,
                    receiverUserId: l3.Value,
                    sourceUserId: round.UserId,
                    level: 3,
                    amount: Math.Round(lossBase * PCT_L3, 8),
                    basisAmount: lossBase,
                    roundId: round.Id,
                    ledgerType: "affiliate_lv3",
                    percent: 2
                );
            }
        });
    }

    // ============================================================
    //  Buscar usuário e uplines
    // ============================================================

    private async Task<_User?> LoadUserAsync(Guid userId)
    {
        string sql = @"
            SELECT Id, InviterL1Id, InviterL2Id, InviterL3Id
            FROM Users
            WHERE Id = @id
        ";

        using var reader = await _db.ExecuteQueryAsync(sql, new NpgsqlParameter("@id", userId));
        if (!await reader.ReadAsync()) return null;

        return new _User
        {
            Id = reader.GetInt64(0),
            InviterL1 = reader.IsDBNull(1) ? null : reader.GetGuid(1),
            InviterL2 = reader.IsDBNull(2) ? null : reader.GetGuid(2),
            InviterL3 = reader.IsDBNull(3) ? null : reader.GetGuid(3)
        };
    }

    // ============================================================
    //  Criar payout, ledger e atualizar wallet
    // ============================================================

    private async Task CreatePayoutAsync(
        NpgsqlTransaction tx,
        Guid receiverUserId,
        Guid sourceUserId,
        short level,
        decimal amount,
        decimal basisAmount,
        Guid roundId,
        string ledgerType,
        int percent
    )
    {
        if (amount <= 0m) return;

        // =============================
        // Garantir que wallet existe
        // =============================
        int walletId = await EnsureWalletAsync(tx, receiverUserId);

        // =============================
        // Registrar payout
        // =============================
        string insertPayoutSql = @"
            INSERT INTO AffiliatePayouts (
                Id, ReceiverUserId, SourceUserId, Level, Amount, BasisAmount, RoundId, CreatedAt
            ) VALUES (
                @id, @ruser, @suser, @level, @amount, @basis, @round, NOW()
            );
        ";

        await _db.ExecuteNonQueryAsync(insertPayoutSql,
            new NpgsqlParameter("@id", Guid.NewGuid()),
            new NpgsqlParameter("@ruser", receiverUserId),
            new NpgsqlParameter("@suser", sourceUserId),
            new NpgsqlParameter("@level", level),
            new NpgsqlParameter("@amount", amount),
            new NpgsqlParameter("@basis", basisAmount),
            new NpgsqlParameter("@round", roundId)
        );

        // =============================
        // Criar ledger
        // =============================
        string metadata = JsonSerializer.Serialize(new { basisAmount, percent });

        string insertLedgerSql = @"
            INSERT INTO WalletLedgers (
                Id, WalletId, Type, Amount, SourceUserId, RoundId, Metadata
            ) VALUES (
                @id, @wallet, @type, @amount, @src, @round, @meta
            );
        ";

        await _db.ExecuteNonQueryAsync(insertLedgerSql,
            new NpgsqlParameter("@id", Guid.NewGuid()),
            new NpgsqlParameter("@wallet", walletId),
            new NpgsqlParameter("@type", ledgerType),
            new NpgsqlParameter("@amount", amount),
            new NpgsqlParameter("@src", sourceUserId),
            new NpgsqlParameter("@round", roundId),
            new NpgsqlParameter("@meta", metadata)
        );

        // =============================
        // Atualizar saldo da wallet
        // =============================
        string updateWalletSql = @"
            UPDATE Wallets SET Balance = Balance + @amt
            WHERE Id = @id
        ";

        await _db.ExecuteNonQueryAsync(updateWalletSql,
            new NpgsqlParameter("@amt", amount),
            new NpgsqlParameter("@id", walletId)
        );
    }

    // ============================================================
    //  Criar wallet caso não exista
    // ============================================================

    private async Task<int> EnsureWalletAsync(NpgsqlTransaction tx, Guid userId)
    {
        string sqlFind = @"SELECT Id FROM Wallets WHERE UserId = @uid";
        using var reader = await _db.ExecuteQueryAsync(sqlFind, new NpgsqlParameter("@uid", userId));

        if (await reader.ReadAsync())
            return reader.GetInt32(0);

        // Criar wallet
        string sqlInsert = @"INSERT INTO Wallets (UserId, Balance)
                             VALUES (@uid, 0)
                             RETURNING Id";

        int newId = await _db.ExecuteScalarAsync<int>(sqlInsert, new NpgsqlParameter("@uid", userId));
        return newId;
    }
}
