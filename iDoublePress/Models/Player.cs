namespace iDoublePress.Models;

/// <summary>
/// Represents a golf player.
/// </summary>
public class Player
{
    public int ID { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Handicap { get; set; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public override string ToString() => Name;
}
