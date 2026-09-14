namespace iDoublePress.Models;

/// <summary>
/// Represents a golf player.
/// </summary>
public class Player : ISyncEntity
{
    public int ID { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Handicap { get; set; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string PublicId { get; set; } = string.Empty;
    public DateTime SyncUpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? ServerRevision { get; set; }
    public bool PendingSync { get; set; }

    public override string ToString() => Name;
}
