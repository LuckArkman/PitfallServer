using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DTOs;

public class AffiliatePayout
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ReceiverUserId { get; set; }

    [ForeignKey(nameof(ReceiverUserId))]
    public User ReceiverUser { get; set; }

    [Required]
    public Guid SourceUserId { get; set; } // jogador que perdeu

    public short Level { get; set; } // 1,2,3

    [Column(TypeName = "decimal(18,8)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,8)")]
    public decimal BasisAmount { get; set; } // loss_base

    public Guid? RoundId { get; set; } // opcional

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}