namespace iDoublePress.Models;

/// <summary>
/// Represents a golf round.
/// </summary>
public class Round
{
    public int ID { get; set; }
    public int PlayerID { get; set; }
    public int CourseID { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int TotalScore { get; set; }
    public RoundStatus Status { get; set; }
    public string? Notes { get; set; }
    public string? Weather { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Player? Player { get; set; }
    public Course? Course { get; set; }
    public List<Hole> Holes { get; set; } = new();

    // Calculated properties
    public int ScoreRelativeToPar
    {
        get
        {
            var scoredHoles = Holes.Where(h => h.IsScored).ToList();
            if (!scoredHoles.Any())
                return 0;

            var totalScore = scoredHoles.Sum(h => h.Score);
            var totalPar = scoredHoles.Sum(h => h.Par);
            return totalScore - totalPar;
        }
    }
    public TimeSpan Duration => (EndTime ?? DateTime.Now) - StartTime;

    public string ScoreDisplay => ScoreRelativeToPar switch
    {
        0 => "E",
        > 0 => $"+{ScoreRelativeToPar}",
        < 0 => ScoreRelativeToPar.ToString()
    };

    public override string ToString() => $"{Course?.Name} - {StartTime:MMM dd, yyyy}";
}

/// <summary>
/// Represents the status of a round.
/// </summary>
public enum RoundStatus
{
    InProgress,
    Completed,
    Abandoned
}
