using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace RevitUI.UI.RoomSheet
{
    public partial class RoomSheetDashboard : Window
    {
        private readonly ExternalEvent _externalEvent;
        private readonly RoomSheetHandler _handler;
        private readonly UIApplication _uiApp;
        private List<LevelGroup> _allLevels;

        public RoomSheetDashboard(ExternalEvent externalEvent, RoomSheetHandler handler, UIApplication uiApp)
        {
            InitializeComponent();
            this.HideIcon();
            _externalEvent = externalEvent;
            _handler = handler;
            _uiApp = uiApp;

            LoadRooms();
            LoadTitleblocks();

            _handler.OnSuccess = (sheetNum) => {
                TaskDialog.Show("B-Lab", "Sheet " + sheetNum + " created successfully!");
            };
        }

        private void LoadRooms()
        {
            Document doc = _uiApp.ActiveUIDocument.Document;
            var rooms = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .Cast<Room>()
                .Where(r => r.Area > 0 && r.Level != null)
                .ToList();

            _allLevels = rooms.GroupBy(r => r.Level.Name)
                .Select(g => new LevelGroup { 
                    Name = g.Key, 
                    Rooms = g.Select(r => new RoomItem { 
                        Id = r.Id, 
                        Name = r.Name, 
                        Number = r.Number 
                    }).OrderBy(r => r.Number).ToList() 
                })
                .OrderBy(l => l.Name)
                .ToList();

            TreeRooms.ItemsSource = _allLevels;
        }

        private void SearchRoom_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string filter = TxtSearchRoom.Text.ToLower();
            if (string.IsNullOrWhiteSpace(filter))
            {
                TreeRooms.ItemsSource = _allLevels;
                return;
            }

            var filtered = _allLevels.Select(l => new LevelGroup {
                Name = l.Name,
                Rooms = l.Rooms.Where(r => r.Name.ToLower().Contains(filter) || r.Number.ToLower().Contains(filter)).ToList()
            }).Where(l => l.Rooms.Any()).ToList();

            TreeRooms.ItemsSource = filtered;
        }

        private void TreeRooms_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is RoomItem room)
            {
                _handler.SelectedRoomId = room.Id;
                TxtSheetNumber.Text = "RDS-" + room.Number;
                TxtSheetName.Text = "RDS - " + room.Name.ToUpper();
            }
        }

        private void LoadTitleblocks()
        {
            var titleblocks = new FilteredElementCollector(_uiApp.ActiveUIDocument.Document)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsElementType()
                .Cast<FamilySymbol>()
                .ToList();
            
            ComboTitleblock.ItemsSource = titleblocks;
            if (titleblocks.Count > 0) ComboTitleblock.SelectedIndex = 0;
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            if (_handler.SelectedRoomId == null)
            {
                TaskDialog.Show("B-Lab", "Please select a room from the list first.");
                return;
            }

            _handler.SheetNumber = TxtSheetNumber.Text;
            _handler.SheetName = TxtSheetName.Text;
            
            _handler.CreateAllElevations = ChkAllElevations.IsChecked == true;
            _handler.Create3DView = Chk3DView.IsChecked == true;

            string scaleStr = (ComboScale.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content.ToString();
            if (scaleStr != null && scaleStr.Contains(":"))
            {
                _handler.ScaleValue = int.Parse(scaleStr.Split(':')[1]);
            }

            if (ComboTitleblock.SelectedItem is FamilySymbol symbol)
            {
                _handler.TitleBlockId = symbol.Id;
            }

            _externalEvent.Raise();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            RevitUI.Command.RoomSheetCommand.Instance = null;
        }

        public class LevelGroup { public string Name { get; set; } public List<RoomItem> Rooms { get; set; } }
        public class RoomItem { public ElementId Id { get; set; } public string Name { get; set; } public string Number { get; set; } }
    }
}
