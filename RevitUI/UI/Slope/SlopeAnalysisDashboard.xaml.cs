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
        }

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            _handler.AnalyzeFloors = ChkFloors.IsChecked == true;
            _handler.AnalyzeRoofs = ChkRoofs.IsChecked == true;
            _handler.AnalyzeTopo = ChkTopo.IsChecked == true;
            _handler.ApplyColor = RbHeatMap.IsChecked == true;

            _externalEvent.Raise();
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
