using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DTOs;

public class _WalletLedger
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WalletId { get; set; }
    
    [ForeignKey(nameof(WalletId))]
    public Wallet Wallet { get; set; }

    public string Type { get; set; }

    [Column(TypeName = "decimal(18,8)")]
    public decimal Amount { get; set; }

    public Guid SourceUserId { get; set; }
    public Guid? RoundId { get; set; }

    public string Metadata { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}