using System;
using System.Windows;
using Autodesk.Revit.UI;

namespace RevitUI.UI.Dimensioning
{
    public partial class DimensionDashboard : Window
    {
        private readonly ExternalEvent _externalEvent;
        private readonly DimensionHandler _handler;

        public DimensionDashboard(ExternalEvent externalEvent, DimensionHandler handler)
        {
            InitializeComponent();
            this.HideIcon(); // Standardized B-Lab look
            _externalEvent = externalEvent;
            _handler = handler;
        }

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            // Set parameters in the handler
            _handler.Mode = RbGrids.IsChecked == true ? DimMode.Grids :
                           RbWalls.IsChecked == true ? DimMode.Walls :
                           RbRooms.IsChecked == true ? DimMode.Rooms :
                           RbColumns.IsChecked == true ? DimMode.Columns :
                           DimMode.MEP;
            
            if (double.TryParse(TxtOffset.Text, out double offset))
                _handler.OffsetMm = offset;

            _handler.IncludeHost = CbIncludeHost.IsChecked == true;
            _handler.IncludeLinked = CbIncludeLinked.IsChecked == true;

            // Trigger the event
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
            // Cleanup: ensure the command knows we're closed
            RevitUI.Command.DimensionCommand.Instance = null;
        }
    }

    public enum DimMode { Grids, Walls, Rooms, MEP, Columns }
}
