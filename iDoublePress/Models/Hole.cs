using CommunityToolkit.Mvvm.ComponentModel;

namespace iDoublePress.Models;

/// <summary>
/// Represents a hole score in a round.
/// </summary>
public partial class Hole : ObservableObject
{
    public int ID { get; set; }
    public int RoundID { get; set; }
    public int HoleNumber { get; set; }
    public int Par { get; set; }

    [ObservableProperty]
    private int score;

    [ObservableProperty]
    private bool isScored;

    public int Putts { get; set; }

    [ObservableProperty]
    private FairwayResult fairwayResult = FairwayResult.None;

    // If the tee shot missed (Left/Right) and this is checked, treat as OB/Hazard miss.
    public bool FairwayMissPenalty { get; set; }

    [ObservableProperty]
    private bool? greenInRegulation;

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

    public int FairwayResultIndex
    {
        get => (int)FairwayResult - 1;
        set
        {
            var enumValue = value < 0 ? FairwayResult.None : (FairwayResult)(value + 1);
            if (FairwayResult != enumValue)
            {
                FairwayResult = enumValue;
                OnPropertyChanged(nameof(FairwayResultIndex));
            }
        }
    }

    partial void OnFairwayResultChanged(FairwayResult value)
    {
        OnPropertyChanged(nameof(FairwayResultIndex));
    }
}

public enum FairwayResult
{
    None = 0,
    Left = 1,
    Fairway = 2,
    Right = 3
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
