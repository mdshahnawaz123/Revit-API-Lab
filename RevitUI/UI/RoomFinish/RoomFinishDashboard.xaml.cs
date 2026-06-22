using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace RevitUI.UI.RoomFinish
{
    public partial class RoomFinishDashboard : Window
    {
        private ExternalEvent _externalEvent;
        private RoomFinishHandler _handler;
        private Document _doc;

        public RoomFinishDashboard(ExternalEvent externalEvent, RoomFinishHandler handler, Document doc)
        {
            InitializeComponent();
            _externalEvent = externalEvent;
            _handler = handler;
            _doc = doc;

            PopulateTypes();
        }

        private void PopulateTypes()
        {
            try
            {
                var floorTypes = new FilteredElementCollector(_doc)
                    .OfClass(typeof(FloorType))
                    .Cast<FloorType>()
                    .Where(f => !f.IsFoundationSlab)
                    .OrderBy(f => f.Name)
                    .ToList();

                ComboFloorType.ItemsSource = floorTypes;
                ComboFloorType.DisplayMemberPath = "Name";
                if (floorTypes.Any()) ComboFloorType.SelectedIndex = 0;

                var wallTypes = new FilteredElementCollector(_doc)
                    .OfClass(typeof(WallType))
                    .Cast<WallType>()
                    .Where(w => w.Kind == WallKind.Basic)
                    .OrderBy(w => w.Name)
                    .ToList();

                ComboWallType.ItemsSource = wallTypes;
                ComboWallType.DisplayMemberPath = "Name";
                if (wallTypes.Any()) ComboWallType.SelectedIndex = 0;
            }
            catch { }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            this.DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            RevitUI.Command.RoomFinishCommand.Instance = null;
        }

        private void PopulateHandlerSettings()
        {
            _handler.CreateFloorFinish = CbCreateFloor.IsChecked == true;
            _handler.CreateWallFinish = CbCreateWall.IsChecked == true;

            if (ComboFloorType.SelectedItem is FloorType ft)
                _handler.FloorTypeId = ft.Id;
            else
                _handler.FloorTypeId = ElementId.InvalidElementId;

            if (ComboWallType.SelectedItem is WallType wt)
                _handler.WallTypeId = wt.Id;
            else
                _handler.WallTypeId = ElementId.InvalidElementId;

            _handler.FindCeilingHost = CbFindCeilingHost.IsChecked == true;
            _handler.FindCeilingLinked = CbFindCeilingLinked.IsChecked == true;
            
            if (double.TryParse(TxtHeightOverride.Text, out double overrideHeight))
                _handler.HeightOverrideMm = overrideHeight;
            else
                _handler.HeightOverrideMm = -1; // -1 means auto

            if (RbScopeSelection.IsChecked == true) _handler.Scope = RoomScope.Selection;
            else if (RbScopeLevel.IsChecked == true) _handler.Scope = RoomScope.Level;
            else if (RbScopeHost.IsChecked == true) _handler.Scope = RoomScope.Host;
            else if (RbScopeLinked.IsChecked == true) _handler.Scope = RoomScope.Linked;
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            PopulateHandlerSettings();
            _handler.IsSyncMode = false;
            _externalEvent.Raise();
        }

        private void BtnSync_Click(object sender, RoutedEventArgs e)
        {
            PopulateHandlerSettings();
            _handler.IsSyncMode = true;
            _externalEvent.Raise();
        }
    }

    public enum RoomScope
    {
        Selection,
        Level,
        Host,
        Linked
    }
}
