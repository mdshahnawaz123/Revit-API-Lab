using System;
using System.Windows;
using System.Windows.Media;
using Autodesk.Revit.UI;
using RevitUI.Command;
using RevitUI.ExternalCommand.ModelHealth;
using RevitUI.UI;

namespace RevitUI.UI.ModelHealth
{
    public partial class ModelHealthDashboard : Window
    {
        private readonly ExternalEvent _externalEvent;
        private readonly ModelHealthHandler _handler;

        public ModelHealthDashboard(ExternalEvent externalEvent, ModelHealthHandler handler)
        {
            InitializeComponent();
            this.HideIcon();
            _externalEvent = externalEvent;
            _handler = handler;
            
            // Trigger initial scan
            _externalEvent.Raise();
        }

        public void UpdateMetrics(ModelHealthData data)
        {
            ScoreText.Text = ((int)data.HealthScore).ToString();
            WarningCount.Text = data.WarningCount.ToString();
            InPlaceCount.Text = data.InPlaceCount.ToString();
            GroupCount.Text = data.GroupCount.ToString();
            CadCount.Text = data.CadImportCount.ToString();
            RoomCount.Text = data.RedundantRoomCount.ToString();
            ViewCount.Text = data.OrphanedViewCount.ToString();
            LinkCount.Text = data.LinkCount.ToString();
            FilterCount.Text = data.UnusedFilterCount.ToString();

            // Update Status color and text
            if (data.HealthScore > 80)
            {
                HealthStatus.Text = "Excellent";
                HealthStatus.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green
            }
            else if (data.HealthScore > 50)
            {
                HealthStatus.Text = "Attention Needed";
                HealthStatus.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Amber
            }
            else
            {
                HealthStatus.Text = "Critical";
                HealthStatus.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            _externalEvent.Raise();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            ModelHealthCommand.Instance = null;
        }
    }
}
