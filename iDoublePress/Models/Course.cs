namespace iDoublePress.Models;

/// <summary>
/// Represents a golf course.
/// </summary>
public class Course : ISyncEntity
{
    public int ID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public int TotalPar { get; set; }
    public int Holes { get; set; } = 18;
    public decimal? Rating { get; set; }
    public int? Slope { get; set; }
    public bool IsCustom { get; set; }
    public DateTime CreatedAt { get; set; }
    public string PublicId { get; set; } = string.Empty;
    public DateTime SyncUpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? ServerRevision { get; set; }
    public bool PendingSync { get; set; }

    public List<CourseHole> CourseHoles { get; set; } = new();

    public override string ToString() => Name;
}

/// <summary>
/// Represents a hole on a golf course.
/// </summary>
public partial class CourseHole : CommunityToolkit.Mvvm.ComponentModel.ObservableObject, ISyncEntity
{
    public int ID { get; set; }
    public int CourseID { get; set; }

    public int HoleNumber { get; set; }

    private int _par;
    public int Par
    {
        get => _par;
        set => SetProperty(ref _par, value);
    }

    public int? Handicap { get; set; }
    public int? Yardage { get; set; }
    public string PublicId { get; set; } = string.Empty;
    public DateTime SyncUpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? ServerRevision { get; set; }
    public bool PendingSync { get; set; }
}
