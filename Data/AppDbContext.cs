using DTOs;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Admin> Admins { get; set; }
    public DbSet<Wallet> Wallets { get; set; }
    public DbSet<WalletLedger> WalletLedger { get; set; }
    public DbSet<GameRound> GameRounds { get; set; }
    public DbSet<PixTransaction> PixTransactions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User configuration
        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("users");
            b.HasKey(x => x.Id).HasName("pk_users");
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.Email).HasColumnName("email").IsRequired().HasMaxLength(255);
            b.HasIndex(x => x.Email).IsUnique().HasDatabaseName("ix_users_email");
            b.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(255);
            b.Property(x => x.PasswordHash).HasColumnName("password_hash").IsRequired();
            b.Property(x => x.IsInfluencer).HasColumnName("is_influencer").HasDefaultValue(false);
            b.Property(x => x.Status).HasColumnName("status").HasDefaultValue("active").HasMaxLength(50);
            b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        });

        // Admin configuration
        modelBuilder.Entity<Admin>(b =>
        {
            b.ToTable("admins");
            b.HasKey(x => x.Id).HasName("pk_admins");
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.Email).HasColumnName("email").IsRequired().HasMaxLength(255);
            b.Property(x => x.PasswordHash).HasColumnName("password_hash").IsRequired();
            b.Property(x => x.Role).HasColumnName("role").IsRequired().HasDefaultValue("Administrator");
            b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            b.Property(x => x.LastLoginAt).HasColumnName("last_login_at");
            b.HasIndex(x => x.Email).IsUnique().HasDatabaseName("ix_admins_email");
        });

        // Wallet configuration
        modelBuilder.Entity<Wallet>(b =>
        {
            b.ToTable("wallets");
            b.HasKey(w => w.UserId).HasName("pk_wallets");
            b.Property(w => w.UserId).HasColumnName("user_id");
            b.Property(w => w.Currency).HasColumnName("currency").IsRequired().HasDefaultValue("BRL");
            b.Property(w => w.Balance).HasColumnName("balance").IsRequired().HasDefaultValue(0m).HasPrecision(18, 2);
            b.Property(w => w.BalanceWithdrawal).HasColumnName("balance_withdrawal").IsRequired().HasDefaultValue(0m).HasPrecision(18, 2);
            b.Property(w => w.BalanceBonus).HasColumnName("balance_bonus").IsRequired().HasDefaultValue(0m).HasPrecision(18, 2);
            b.Property(w => w.UpdatedAt).HasColumnName("updated_at").IsRequired();
            b.HasOne(w => w.User).WithOne(u => u.Wallet).HasForeignKey<Wallet>(w => w.UserId).HasConstraintName("fk_wallets_user_id");
        });

        // WalletLedger configuration
        modelBuilder.Entity<WalletLedger>(b =>
        {
            b.ToTable("wallet_ledger");
            b.HasKey(x => x.Id).HasName("pk_wallet_ledger");
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            b.Property(x => x.Type).HasColumnName("entry_type").IsRequired();
            b.Property(x => x.Amount).HasColumnName("amount").IsRequired().HasPrecision(18, 2);
            b.Property(x => x.BalanceAfter).HasColumnName("balance_after").IsRequired().HasPrecision(18, 2);
            b.Property(x => x.GameRoundId).HasColumnName("game_round_id");
            b.Property(x => x.Metadata).HasColumnName("metadata").IsRequired();
            b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).HasConstraintName("fk_wallet_ledger_user_id");
            b.HasOne<GameRound>().WithMany().HasForeignKey(x => x.GameRoundId).HasConstraintName("fk_wallet_ledger_game_round_id");
        });

        // GameRound configuration
        modelBuilder.Entity<GameRound>(b =>
        {
            b.ToTable("game_rounds");
            b.HasKey(x => x.Id).HasName("pk_game_rounds");
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            b.Property(x => x.BetAmount).HasColumnName("bet_amount").IsRequired().HasPrecision(18, 2);
            b.Property(x => x.PrizeAmount).HasColumnName("prize_amount").IsRequired().HasPrecision(18, 2);
            b.Property(x => x.Result).HasColumnName("game_result").IsRequired();
            b.Property(x => x.TrapPositions).HasColumnName("trap_positions").IsRequired().HasConversion(
                v => JsonSerializer.Serialize<int[]>(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<int[]>(v, (JsonSerializerOptions?)null) ?? Array.Empty<int>());
            b.Property(x => x.OpenedPositions).HasColumnName("opened_positions").IsRequired().HasConversion(
                v => JsonSerializer.Serialize<int[]>(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<int[]>(v, (JsonSerializerOptions?)null) ?? Array.Empty<int>());
            b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).HasConstraintName("fk_game_rounds_user_id");
        });

        modelBuilder.Entity<PixTransaction>(b =>
        {
            b.ToTable("pix_transactions");
            b.HasKey(x => x.Id).HasName("pk_pix_transactions");
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            b.Property(x => x.Type).HasColumnName("type").IsRequired().HasDefaultValue("PIX_IN");
            b.Property(x => x.IdTransaction).HasColumnName("id_transaction").IsRequired();
            b.Property(x => x.Amount).HasColumnName("amount").IsRequired().HasPrecision(18, 2);
            b.Property(x => x.Status).HasColumnName("status").IsRequired().HasDefaultValue("pending");
            b.Property(x => x.PixKey).HasColumnName("pix_key").IsRequired();
            b.Property(x => x.PixKeyType).HasColumnName("pix_key_type").IsRequired();
            b.Property(x => x.QrCode).HasColumnName("qr_code").IsRequired();
            b.Property(x => x.QrCodeImageUrl).HasColumnName("qr_code_image_url").IsRequired();
            b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            b.Property(x => x.PaidAt).HasColumnName("paid_at");
            b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).HasConstraintName("fk_pix_transactions_user_id");
            b.HasIndex(x => x.IdTransaction).IsUnique().HasDatabaseName("ix_pix_transactions_id_transaction");
        });

        base.OnModelCreating(modelBuilder);
    }
}