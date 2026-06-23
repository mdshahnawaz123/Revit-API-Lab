using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
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

        private void RbScope_Checked(object sender, RoutedEventArgs e)
        {
            if (PanelRoomSelect == null || ComboRooms == null) return;

            if (RbScopeHost.IsChecked == true || RbScopeLinked.IsChecked == true)
            {
                PanelRoomSelect.Visibility = System.Windows.Visibility.Visible;
                PopulateRooms();
            }
            else
            {
                PanelRoomSelect.Visibility = System.Windows.Visibility.Collapsed;
                ComboRooms.ItemsSource = null;
            }
        }

        private void PopulateRooms()
        {
            if (_doc == null) return;
            bool useLinked = RbScopeLinked.IsChecked == true;
            var rooms = new System.Collections.Generic.List<Room>();

            if (useLinked)
            {
                var linkInstances = new FilteredElementCollector(_doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .ToList();

                foreach (var link in linkInstances)
                {
                    Document linkDoc = link.GetLinkDocument();
                    if (linkDoc != null)
                    {
                        rooms.AddRange(new FilteredElementCollector(linkDoc)
                            .OfCategory(BuiltInCategory.OST_Rooms)
                            .Cast<Room>()
                            .Where(r => r.Area > 0));
                    }
                }
            }
            
            // Host model rooms
            rooms.AddRange(new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .Cast<Room>()
                .Where(r => r.Area > 0));

            ComboRooms.ItemsSource = rooms.OrderBy(r => r.Name).ToList();
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

            if (ComboRooms.SelectedItem is Room selectedRoom && PanelRoomSelect.Visibility == System.Windows.Visibility.Visible)
                _handler.SelectedRoomId = selectedRoom.Id;
            else
                _handler.SelectedRoomId = ElementId.InvalidElementId;
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
