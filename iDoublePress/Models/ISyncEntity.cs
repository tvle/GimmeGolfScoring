namespace iDoublePress.Models;

public interface ISyncEntity
{
    string PublicId { get; set; }
    DateTime SyncUpdatedAtUtc { get; set; }
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
    string? ServerRevision { get; set; }
    bool PendingSync { get; set; }
}
