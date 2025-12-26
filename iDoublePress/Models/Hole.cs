namespace iDoublePress.Models;

/// <summary>
/// Represents a hole score in a round.
/// </summary>
public class Hole
{
    public int ID { get; set; }
    public int RoundID { get; set; }
    public int HoleNumber { get; set; }
    public int Par { get; set; }
    public int Score { get; set; }
    public int Putts { get; set; }
    public bool? FairwayHit { get; set; }
    public bool? GreenInRegulation { get; set; }
    public int Penalties { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Calculated properties
    public int ScoreRelativeToPar => Score - Par;
    public ScoreType ScoreTypeEnum => CalculateScoreType();

    private ScoreType CalculateScoreType()
    {
        var diff = ScoreRelativeToPar;
        return diff switch
        {
            <= -2 => ScoreType.Eagle,
            -1 => ScoreType.Birdie,
            0 => ScoreType.Par,
            1 => ScoreType.Bogey,
            _ => ScoreType.DoubleBogeyOrWorse
        };
    }

    public Color ScoreColor => ScoreTypeEnum switch
    {
        ScoreType.Eagle => Color.FromArgb("#2D7A3E"),
        ScoreType.Birdie => Color.FromArgb("#4CAF50"),
        ScoreType.Par => Color.FromArgb("#FFC107"),
        ScoreType.Bogey => Color.FromArgb("#FF9800"),
        _ => Color.FromArgb("#F44336")
    };

    public string AccessibilityDescription => 
        $"Hole {HoleNumber}, Par {Par}, Score {Score}, {ScoreRelativeToPar switch
        {
            0 => "Par",
            1 => "One over par",
            -1 => "One under par",
            > 1 => $"{ScoreRelativeToPar} over par",
            < -1 => $"{Math.Abs(ScoreRelativeToPar)} under par"
        }}";
}

/// <summary>
/// Represents the type of score relative to par.
/// </summary>
public enum ScoreType
{
    Eagle,
    Birdie,
    Par,
    Bogey,
    DoubleBogeyOrWorse
}
