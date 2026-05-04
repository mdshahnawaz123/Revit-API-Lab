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
        private List<RoomItem> _allRooms = new List<RoomItem>();

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
                // TaskDialog.Show("B-Lab", "Sheet " + sheetNum + " created successfully!");
            };
        }

        private void LoadRooms()
        {
            Document doc = _uiApp.ActiveUIDocument.Document;
            _allRooms.Clear();

            int scope = ComboScope.SelectedIndex; // 0: Active View, 1: All Model, 2: Links

            // 1. Current Document Rooms
            var collector = (scope == 0) 
                ? new FilteredElementCollector(doc, doc.ActiveView.Id) 
                : new FilteredElementCollector(doc);

            var rooms = collector.OfCategory(BuiltInCategory.OST_Rooms)
                .Cast<Room>()
                .Where(r => r.Area > 0 && r.Level != null)
                .Select(r => new RoomItem { 
                    Id = r.Id, 
                    Name = r.Name, 
                    Number = r.Number, 
                    LevelName = r.Level?.Name ?? "N/A",
                    IsFromLink = false,
                    SourceDoc = doc
                });

            _allRooms.AddRange(rooms);

            // 2. Linked Document Rooms
            if (scope == 2)
            {
                var links = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>();
                foreach (var link in links)
                {
                    Document linkDoc = link.GetLinkDocument();
                    if (linkDoc == null) continue;

                    var linkRooms = new FilteredElementCollector(linkDoc)
                        .OfCategory(BuiltInCategory.OST_Rooms)
                        .Cast<Room>()
                        .Where(r => r.Area > 0 && r.Level != null)
                        .Select(r => new RoomItem { 
                            Id = r.Id, 
                            Name = r.Name, 
                            Number = r.Number, 
                            LevelName = r.Level?.Name ?? "N/A",
                            IsFromLink = true,
                            SourceDoc = linkDoc
                        });
                    _allRooms.AddRange(linkRooms);
                }
            }

            ListRooms.ItemsSource = _allRooms.OrderBy(r => r.Number).ToList();
        }

        private void ComboScope_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (IsLoaded) LoadRooms();
        }

        private void SearchRoom_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string filter = TxtSearchRoom.Text.ToLower();
            if (string.IsNullOrWhiteSpace(filter))
            {
                ListRooms.ItemsSource = _allRooms.OrderBy(r => r.Number).ToList();
                return;
            }

            var filtered = _allRooms.Where(r => 
                r.Name.ToLower().Contains(filter) || 
                r.Number.ToLower().Contains(filter) || 
                r.LevelName.ToLower().Contains(filter))
                .OrderBy(r => r.Number).ToList();

            ListRooms.ItemsSource = filtered;
        }

        private void ChkSelectAll_Checked(object sender, RoutedEventArgs e)
        {
            bool isChecked = ChkSelectAll.IsChecked == true;
            foreach (var item in ListRooms.ItemsSource as List<RoomItem>)
            {
                item.IsSelected = isChecked;
            }
            ListRooms.Items.Refresh();
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
            var selected = (ListRooms.ItemsSource as List<RoomItem>).Where(r => r.IsSelected).ToList();
            if (!selected.Any())
            {
                TaskDialog.Show("B-Lab", "Please select at least one room from the list.");
                return;
            }

            _handler.SelectedRooms = selected;
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
            RevitUI.Command.RoomSheetCommand.Instance = null;
        }

        public class RoomItem : System.ComponentModel.INotifyPropertyChanged
        {
            private bool _isSelected;
            public bool IsSelected 
            { 
                get => _isSelected; 
                set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); } 
            }

            public ElementId Id { get; set; }
            public string Name { get; set; }
            public string Number { get; set; }
            public string LevelName { get; set; }
            public bool IsFromLink { get; set; }
            public Document SourceDoc { get; set; }

            public System.Windows.Visibility LinkedIndicatorVisibility => IsFromLink ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

            public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }
    }
}
