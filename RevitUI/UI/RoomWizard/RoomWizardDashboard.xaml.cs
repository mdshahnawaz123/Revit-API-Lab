using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace RevitUI.UI.RoomWizard
{
    public partial class RoomWizardDashboard : Window
    {
        private ExternalEvent _externalEvent;
        private RoomWizardHandler _handler;
        private UIDocument _uidoc;
        private Document _doc;
        private DataTable _elementData;

        public RoomWizardDashboard(ExternalEvent externalEvent, RoomWizardHandler handler, UIDocument uidoc)
        {
            InitializeComponent();
            _externalEvent = externalEvent;
            _handler = handler;
            _uidoc = uidoc;
            _doc = uidoc.Document;

            LoadRooms();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            RevitUI.Command.RoomWizardCommand.Instance = null;
            this.Close();
        }

        private void ComboRoomSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_doc != null)
                LoadRooms();
        }

        private void ComboRooms_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_doc != null && ComboRooms.SelectedItem != null)
                LoadElements();
        }

        private void LoadRooms()
        {
            bool useLinked = ComboRoomSource.SelectedIndex == 1;
            List<Room> rooms = new List<Room>();

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
            else
            {
                rooms = new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .Cast<Room>()
                    .Where(r => r.Area > 0)
                    .ToList();
            }

            ComboRooms.ItemsSource = rooms.OrderBy(r => r.Name).ToList();
            if (rooms.Any()) ComboRooms.SelectedIndex = 0;
        }

        private void LoadElements()
        {
            _elementData = new DataTable();
            _elementData.Columns.Add("ElementId");
            _elementData.Columns.Add("Category");
            _elementData.Columns.Add("Name");
            _elementData.Columns.Add("RoomName");
            _elementData.Columns.Add("Comments");
            _elementData.Columns.Add("Mark");

            Room selectedRoom = ComboRooms.SelectedItem as Room;
            if (selectedRoom == null) return;

            // Quick mapping: get elements that have a Room property set
            // For a more comprehensive tool, BoundingBoxIntersectsFilter could be used.
            var allElements = new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType()
                .WhereElementIsViewIndependent()
                .ToElements();

            foreach (var elem in allElements)
            {
                if (elem.Category == null) continue;
                if (elem is FamilyInstance fi)
                {
                    Room r = fi.Room ?? fi.FromRoom ?? fi.ToRoom;
                    if (r != null && (r.Id == selectedRoom.Id || r.Name == selectedRoom.Name))
                    {
                        var row = _elementData.NewRow();
                        row["ElementId"] = elem.Id.ToString();
                        row["Category"] = elem.Category.Name;
                        row["Name"] = elem.Name;
                        row["RoomName"] = r.Name;
                        row["Comments"] = elem.LookupParameter("Comments")?.AsString() ?? "";
                        row["Mark"] = elem.LookupParameter("Mark")?.AsString() ?? "";
                        _elementData.Rows.Add(row);
                    }
                }
            }

            DgElements.ItemsSource = _elementData.DefaultView;
            TxtStatus.Text = $"Found {_elementData.Rows.Count} elements.";
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = "RoomWizardExport",
                    DefaultExt = ".csv",
                    Filter = "CSV Files (*.csv)|*.csv"
                };

                if (dlg.ShowDialog() == true)
                {
                    StringBuilder sb = new StringBuilder();
                    IEnumerable<string> columnNames = _elementData.Columns.Cast<DataColumn>().Select(column => column.ColumnName);
                    sb.AppendLine(string.Join(",", columnNames));

                    foreach (DataRow row in _elementData.Rows)
                    {
                        IEnumerable<string> fields = row.ItemArray.Select(field => 
                            string.Concat("\"", field.ToString().Replace("\"", "\"\""), "\""));
                        sb.AppendLine(string.Join(",", fields));
                    }

                    File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                    TaskDialog.Show("Room Wizard", "Successfully exported data to CSV!");
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog
                {
                    DefaultExt = ".csv",
                    Filter = "CSV Files (*.csv)|*.csv"
                };

                if (dlg.ShowDialog() == true)
                {
                    string[] lines = File.ReadAllLines(dlg.FileName);
                    if (lines.Length <= 1) return;

                    string[] headers = lines[0].Split(',');

                    _handler.Updates.Clear();

                    for (int i = 1; i < lines.Length; i++)
                    {
                        // Basic CSV parsing
                        string[] fields = lines[i].Split(',');
                        if (fields.Length != headers.Length) continue;

                        string rawId = fields[0].Replace("\"", "");
#if NET48
                        if (int.TryParse(rawId, out int idVal))
                        {
                            ElementId eId = new ElementId(idVal);
#else
                        if (long.TryParse(rawId, out long idVal))
                        {
                            ElementId eId = new ElementId(idVal);
#endif
                            var update = new ElementUpdateData { ElementId = eId };
                            
                            // Parse parameters from CSV
                            for (int j = 4; j < headers.Length; j++) // Assuming indices 4+ are editable parameters
                            {
                                string paramName = headers[j].Replace("\"", "");
                                string paramValue = fields[j].Replace("\"", "");
                                update.Parameters[paramName] = paramValue;
                            }
                            _handler.Updates.Add(update);
                        }
                    }

                    TxtStatus.Text = $"Ready to apply {_handler.Updates.Count} updates.";
                    BtnUpdateModel.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#007ACC"));
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }
        }

        private void BtnUpdateModel_Click(object sender, RoutedEventArgs e)
        {
            if (_handler.Updates.Any())
            {
                TxtStatus.Text = "Applying updates...";
                _externalEvent.Raise();
            }
            else
            {
                TaskDialog.Show("Room Wizard", "Please import a modified CSV first.");
            }
        }
    }
}
