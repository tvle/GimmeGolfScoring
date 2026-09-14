using CommunityToolkit.Mvvm.ComponentModel;

namespace iDoublePress.Models
{
    public partial class ShotSegment : ObservableObject, ISyncEntity
    {
        // Database identity and relationship
        public int ID { get; set; }
        public int HoleID { get; set; }
        public int Sequence { get; set; }
        public DateTime CreatedAt { get; set; }
        public string PublicId { get; set; } = string.Empty;
        public DateTime SyncUpdatedAtUtc { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAtUtc { get; set; }
        public string? ServerRevision { get; set; }
        public bool PendingSync { get; set; }

        // Store the raw GPS data for recalculation
        public Location Point { get; set; }

        /// <summary>
        /// Horizontal accuracy of the GPS fix in meters.
        /// Lower is better — values ≤10m are considered good for golf.
        /// </summary>
        public double? AccuracyMeters { get; set; }

        /// <summary>
        /// Human-readable accuracy indicator (e.g. "±3m").
        /// </summary>
        public string AccuracyDisplay => AccuracyMeters.HasValue
            ? $"±{AccuracyMeters.Value:F0}m"
            : "---";

        // Observable so the UI updates when we change "150y" to "165y"
        [ObservableProperty]
        private string distanceDisplay;

        [ObservableProperty]
        private string locationDisplay;

        [ObservableProperty]
        private string tag;
    }
}