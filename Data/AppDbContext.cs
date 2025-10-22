using DTOs;
using Microsoft.EntityFrameworkCore;

namespace Data;

public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Admin> Admins { get; set; }
        public DbSet<Wallet> Wallets { get; set; }
        public DbSet<WalletLedger> WalletLedger { get; set; }
        public DbSet<GameRound> GameRounds { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // map to snake_case names used in schema.sql (optional)
            modelBuilder.Entity<User>(b =>
            {
                b.ToTable("users");
                b.HasKey(x => x.Id).HasName("pk_users");
                b.Property(x => x.Id).HasColumnName("id");
                b.Property(x => x.Email).HasColumnName("email");
                b.HasIndex(x => x.Email).IsUnique().HasDatabaseName("ix_users_email");
                b.Property(x => x.Name).HasColumnName("name");
                b.Property(x => x.PasswordHash).HasColumnName("PasswordHash");
                b.Property(x => x.IsInfluencer).HasColumnName("is_influencer").HasDefaultValue(false);
                b.Property(x => x.Status).HasColumnName("status").HasDefaultValue("active");
            });

            modelBuilder.Entity<Wallet>(b =>
            {
                b.ToTable("wallets");
                b.HasKey(w => w.UserId).HasName("UserId");
                b.Property(w => w.Currency).HasColumnName("Currency");
                b.HasKey(w => w.Balance).HasName("Balance");
                b.Property(w => w.BalanceWithdrawal).HasColumnName("BalanceWithdrawal");
                b.HasKey(w => w.BalanceBonus).HasName("BalanceBonus");
                b.Property(w => w.UpdatedAt).HasColumnName("UpdatedAt");
            });


            modelBuilder.Entity<Wallet>(b =>
            {
                b.ToTable("wallets");
                b.HasKey(w => w.UserId).HasName("UserId");
                b.Property(w => w.Currency).HasColumnName("Currency");
                b.Property(w => w.Balance).HasColumnName("Balance");
                b.Property(w => w.BalanceWithdrawal).HasColumnName("BalanceWithdrawal");
                b.Property(w => w.BalanceBonus).HasColumnName("BalanceBonus");
                b.Property(w => w.UpdatedAt).HasColumnName("UpdatedAt");
            });

            modelBuilder.Entity<WalletLedger>(b =>
            {
                b.ToTable("wallet_ledger");
                b.HasKey(x => x.Id).HasName("pk_wallet_ledger");
                b.Property(x => x.Id).HasColumnName("id");
            });

            modelBuilder.Entity<GameRound>(b =>
            {
                b.ToTable("game_rounds");
                b.HasKey(x => x.Id).HasName("pk_game_rounds");
                b.Property(x => x.Id).HasColumnName("id");
            });

            // NOTE: muitos tipos ENUM e detalhes do schema.sql são criados via migration SQL (usando migrationBuilder.Sql)
            base.OnModelCreating(modelBuilder);
        }
    }