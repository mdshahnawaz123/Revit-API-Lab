using System;
using System.Windows;
using Autodesk.Revit.UI;

namespace RevitUI.UI.Dimensioning
{
    public partial class DimensionDashboard : Window
    {
        private readonly ExternalEvent _externalEvent;
        private readonly DimensionHandler _handler;

        public DimensionDashboard(ExternalEvent externalEvent, DimensionHandler handler, Autodesk.Revit.DB.Document doc)
        {
            InitializeComponent();
            this.HideIcon();
            _externalEvent = externalEvent;
            _handler = handler;

            // Populate Dimension Styles
            var dimStyles = new Autodesk.Revit.DB.FilteredElementCollector(doc)
                .OfClass(typeof(Autodesk.Revit.DB.DimensionType))
                .Cast<Autodesk.Revit.DB.DimensionType>()
                .OrderBy(x => x.Name)
                .ToList();

            foreach (var style in dimStyles)
            {
                ComboDimStyle.Items.Add(new { Name = style.Name, Id = style.Id });
            }
            ComboDimStyle.DisplayMemberPath = "Name";
            if (ComboDimStyle.Items.Count > 0) ComboDimStyle.SelectedIndex = 0;
        }

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            _handler.Mode = ReadSelectedMode();
            
            if (double.TryParse(TxtOffset.Text, out double offset))
                _handler.OffsetMm = offset;

            _handler.IncludeHost = RbIncludeHost.IsChecked == true;
            _handler.IncludeLinked = RbIncludeLinked.IsChecked == true;
            _handler.SameGroup = CbSameGroup.IsChecked == true;
            _handler.UseSelection = RbSelectionOnly.IsChecked == true;
            _handler.MultiTierGrids = CbMultiTier.IsChecked == true;
            string dimRef = (ComboDimRef.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content.ToString() ?? "Faces";
            bool useCenterline = dimRef == "Centerline";
            _handler.WallCoreOnly = useCenterline;
            _handler.RoomUseCenterBoundary = useCenterline;
            _handler.OpeningDimMode = useCenterline ? "Centers" : "Faces";

            if (ComboDimStyle.SelectedItem != null)
            {
                var selected = ComboDimStyle.SelectedItem;
                var prop = selected.GetType().GetProperty("Id");
                if (prop != null)
                {
                    _handler.DimensionStyleId = (Autodesk.Revit.DB.ElementId)prop.GetValue(selected);
                }
            }

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

        /// <summary>Read the checked mode radio (specific modes before default Grids).</summary>
        private DimMode ReadSelectedMode()
        {
            if (RbCurtainWalls.IsChecked == true) return DimMode.CurtainWalls;
            if (RbColumns.IsChecked == true) return DimMode.Columns;
            if (RbMEP.IsChecked == true) return DimMode.MEP;
            if (RbRooms.IsChecked == true) return DimMode.Rooms;
            if (RbWallsFull.IsChecked == true) return DimMode.WallsFull;
            if (RbWalls.IsChecked == true) return DimMode.Walls;
            if (RbGrids.IsChecked == true) return DimMode.Grids;
            return DimMode.Grids;
        }

        private void RbWalls_Checked(object sender, RoutedEventArgs e)
        {

        }
    }

    public enum DimMode { Grids, Walls, WallsFull, Rooms, MEP, Columns, CurtainWalls }
}
