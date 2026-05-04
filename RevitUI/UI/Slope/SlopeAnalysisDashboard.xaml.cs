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
                    PickC1.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(settings.SlopeColors[0]));
                    PickC2.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(settings.SlopeColors[1]));
                    PickC3.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(settings.SlopeColors[2]));
                } catch { }
            }
        }

        private void ColorPicker_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border && border.Background is System.Windows.Media.SolidColorBrush brush)
            {
                var current = brush.Color;
                using (var dialog = new System.Windows.Forms.ColorDialog())
                {
                    dialog.Color = System.Drawing.Color.FromArgb(current.A, current.R, current.G, current.B);
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        var next = dialog.Color;
                        border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(next.A, next.R, next.G, next.B));
                    }
                }
            }
        }

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            _handler.AnalyzeFloors = ChkFloors.IsChecked == true;
            _handler.AnalyzeRoofs = ChkRoofs.IsChecked == true;
            _handler.AnalyzeTopo = ChkTopo.IsChecked == true;
            _handler.ApplyColor = RbHeatMap.IsChecked == true;

            _handler.Unit = ComboUnit.SelectedIndex == 1 ? SlopeUnit.Percentage : SlopeUnit.Degrees;

            var c1 = ((System.Windows.Media.SolidColorBrush)PickC1.Background).Color;
            var c2 = ((System.Windows.Media.SolidColorBrush)PickC2.Background).Color;
            var c3 = ((System.Windows.Media.SolidColorBrush)PickC3.Background).Color;

            _handler.Ranges.Clear();
            _handler.Ranges.Add(CreateRange(TxtT1.Text, c1));
            _handler.Ranges.Add(CreateRange(TxtT2.Text, c2));
            _handler.Ranges.Add(CreateRange(TxtT3.Text, c3));

            // Save Settings
            var settings = DataLab.SettingsManager.Load();
            settings.SlopeThresholds = new System.Collections.Generic.List<double> { 
                double.TryParse(TxtT1.Text, out double t1) ? t1 : 0, 
                double.TryParse(TxtT2.Text, out double t2) ? t2 : 5, 
                double.TryParse(TxtT3.Text, out double t3) ? t3 : 15 
            };
            settings.SlopeColors = new System.Collections.Generic.List<string> {
                c1.ToString(),
                c2.ToString(),
                c3.ToString()
            };
            DataLab.SettingsManager.Save(settings);

            _externalEvent.Raise();
        }

        private SlopeRange CreateRange(string thresholdStr, System.Windows.Media.Color color)
        {
            double threshold = 0;
            double.TryParse(thresholdStr, out threshold);
            return new SlopeRange(threshold, color.R, color.G, color.B);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                this.DragMove();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            RevitUI.Command.SlopeAnalysisCommand.Instance = null;
        }
    }
}
