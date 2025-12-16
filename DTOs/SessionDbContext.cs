using DTOs;
using Microsoft.EntityFrameworkCore;

namespace Data;

public class SessionDbContext : DbContext
{
    public SessionDbContext(DbContextOptions<SessionDbContext> options) : base(options) { }

    public DbSet<UserSession> UserSessions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserSession>(b =>
        {
            b.ToTable("user_sessions");
            b.HasKey(x => x.SessionToken);
            b.Property(x => x.UserId);
            b.Property(x => x.ExpiresAtUtc);
        });
        
        base.OnModelCreating(modelBuilder);
    }
}