using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitUI.ExternalCommand.AutoCadExport;

namespace RevitUI.UI.AutoCadExport
{
    public partial class AutoCadExportUI : Window
    {
        private readonly ExternalEvent _externalEvent;
        private readonly AutoCadExportHandler _handler;
        public ObservableCollection<ViewSelectionItem> Views { get; set; }

        public AutoCadExportUI(UIApplication app, ExternalEvent externalEvent, AutoCadExportHandler handler)
        {
            InitializeComponent();
            _externalEvent = externalEvent;
            _handler = handler;
            
            _handler.Dashboard = this;

            // Load Sheets
            Document doc = app.ActiveUIDocument.Document;

            var sheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .OrderBy(s => s.SheetNumber)
                .Select(s => new ViewSelectionItem { Name = $"{s.SheetNumber} - {s.Name}", Id = s.Id, IsChecked = false })
                .ToList();

            Views = new ObservableCollection<ViewSelectionItem>(sheets);
            ViewListBox.ItemsSource = Views;

            // Default path
            PathTxt.Text = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    PathTxt.Text = dialog.SelectedPath;
                }
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var selected = Views.Where(v => v.IsChecked).Select(v => v.Id).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Please select at least one sheet.", "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(PathTxt.Text))
            {
                MessageBox.Show("Please select an export path.", "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Determine export mode
            ExportMode mode = ExportMode.MultipleLayouts;
            if (ModeModelSpace.IsChecked == true) mode = ExportMode.ModelSpace;
            else if (ModeSingleLayout.IsChecked == true) mode = ExportMode.SingleLayout;
            else if (ModeSeparateFiles.IsChecked == true) mode = ExportMode.SeparateFiles;

            _handler.SelectedViewIds = selected;
            _handler.ExportPath = PathTxt.Text;
            _handler.Mode = mode;
            
            StatusText.Text = $"Exporting {selected.Count} sheets...";
            _externalEvent.Raise();
        }

        public void UpdateStatus(string msg)
        {
            StatusText.Text = msg;
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var v in Views) v.IsChecked = true;
            ViewListBox.Items.Refresh();
        }

        private void SelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var v in Views) v.IsChecked = false;
            ViewListBox.Items.Refresh();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            RevitUI.Command.AutoCadExportCommand.Instance = null;
        }
    }
}
