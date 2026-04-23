using System.ComponentModel;

namespace RevitUI.ExternalCommand.Room3DTag
{
    /// <summary>
    /// View-model row for the "Select one or more Room/Space source models" DataGrid.
    /// </summary>
    public class RoomSourceModel : INotifyPropertyChanged
    {
        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public string ModelName { get; set; }
        public string Category { get; set; }
        public bool VolumeCalcOn { get; set; }
        public int TotalRoomsSpaces { get; set; }
        public int PlacedCorrectly { get; set; }
        public int Redundant { get; set; }
        public int NotPlaced { get; set; }
        public int NotEnclosed { get; set; }
        public int PhaseNumber { get; set; }
        public string Phase { get; set; }
        public string DesignOption { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
