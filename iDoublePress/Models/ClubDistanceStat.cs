namespace iDoublePress.Models
{
    public partial class ClubDistanceStat
    {
        public string ClubName { get; set; }
        public string AverageDistance { get; set; }
        public double SortOrder { get; set; } // Helps us sort Driver -> Wedges
    }
}