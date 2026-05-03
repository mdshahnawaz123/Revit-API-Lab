using System;
using System.Windows;
using Autodesk.Revit.UI;

namespace RevitUI.UI.Slope
{
    public partial class SlopeAnalysisDashboard : Window
    {
        private readonly ExternalEvent _externalEvent;
        private readonly SlopeAnalysisHandler _handler;

        public SlopeAnalysisDashboard(ExternalEvent externalEvent, SlopeAnalysisHandler handler)
        {
            InitializeComponent();
            this.HideIcon();
            _externalEvent = externalEvent;
            _handler = handler;
            LoadSettings();
        }

        private void LoadSettings()
        {
            var settings = DataLab.SettingsManager.Load();
            if (settings.SlopeThresholds?.Count >= 3)
            {
                TxtT1.Text = settings.SlopeThresholds[0].ToString();
                TxtT2.Text = settings.SlopeThresholds[1].ToString();
                TxtT3.Text = settings.SlopeThresholds[2].ToString();
            }
            if (settings.SlopeColors?.Count >= 3)
            {
                try {
                    PickC1.SelectedColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(settings.SlopeColors[0]);
                    PickC2.SelectedColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(settings.SlopeColors[1]);
                    PickC3.SelectedColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(settings.SlopeColors[2]);
                } catch { }
            }
        }

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            _handler.AnalyzeFloors = ChkFloors.IsChecked == true;
            _handler.AnalyzeRoofs = ChkRoofs.IsChecked == true;
            _handler.AnalyzeTopo = ChkTopo.IsChecked == true;
            _handler.ApplyColor = RbHeatMap.IsChecked == true;

            _handler.Unit = ComboUnit.SelectedIndex == 1 ? SlopeUnit.Percentage : SlopeUnit.Degrees;

            _handler.Ranges.Clear();
            _handler.Ranges.Add(CreateRange(TxtT1.Text, PickC1.SelectedColor));
            _handler.Ranges.Add(CreateRange(TxtT2.Text, PickC2.SelectedColor));
            _handler.Ranges.Add(CreateRange(TxtT3.Text, PickC3.SelectedColor));

            // Save Settings
            var settings = DataLab.SettingsManager.Load();
            settings.SlopeThresholds = new System.Collections.Generic.List<double> { 
                double.TryParse(TxtT1.Text, out double t1) ? t1 : 5, 
                double.TryParse(TxtT2.Text, out double t2) ? t2 : 10, 
                double.TryParse(TxtT3.Text, out double t3) ? t3 : 15 
            };
            settings.SlopeColors = new System.Collections.Generic.List<string> {
                PickC1.SelectedColor?.ToString() ?? "#10B981",
                PickC2.SelectedColor?.ToString() ?? "#F59E0B",
                PickC3.SelectedColor?.ToString() ?? "#EF4444"
            };
            DataLab.SettingsManager.Save(settings);

            _externalEvent.Raise();
        }

        private SlopeRange CreateRange(string thresholdStr, System.Windows.Media.Color? color)
        {
            double threshold = 0;
            double.TryParse(thresholdStr, out threshold);

            if (color == null) color = System.Windows.Media.Colors.Gray;
            
            return new SlopeRange(threshold, color.Value.R, color.Value.G, color.Value.B);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            this.DragMove();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            RevitUI.Command.SlopeAnalysisCommand.Instance = null;
        }
    }
}
