namespace iDoublePress.Models;

public class GreenStat
{
    public string ZoneName { get; set; } // "Front", "Center", "Back"
    public int Count { get; set; }
    public double TotalShots { get; set; }

    // Helper to show "5 (25%)"
    public string DisplayValue => TotalShots > 0
        ? $"{Count} ({Count / TotalShots:P0})"
        : $"{Count}";

    // Helper for sorting (Front -> Center -> Back)
    public int Order { get; set; }
}