using CommunityToolkit.Mvvm.ComponentModel;

namespace iDoublePress.Models
{
    public partial class ShotSegment : ObservableObject
    {
        // Store the raw GPS data for recalculation
        public Location Point { get; set; }

        // Observable so the UI updates when we change "150y" to "165y"
        [ObservableProperty]
        private string distanceDisplay;

        [ObservableProperty]
        private string locationDisplay;
    }
}