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
        private ObservableCollection<ViewSelectionItem> _filteredViews;

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
            _filteredViews = new ObservableCollection<ViewSelectionItem>(sheets);
            ViewListBox.ItemsSource = _filteredViews;

            // Default path
            PathTxt.Text = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }

        private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string filter = SearchBox.Text.ToLower();
            if (string.IsNullOrWhiteSpace(filter))
            {
                _filteredViews = new ObservableCollection<ViewSelectionItem>(Views);
            }
            else
            {
                var filtered = Views.Where(v => v.Name.ToLower().Contains(filter)).ToList();
                _filteredViews = new ObservableCollection<ViewSelectionItem>(filtered);
            }
            ViewListBox.ItemsSource = _filteredViews;
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            var selected = ViewListBox.SelectedItem as ViewSelectionItem;
            if (selected == null) return;

            int index = Views.IndexOf(selected);
            if (index > 0)
            {
                Views.Move(index, index - 1);
                // Refresh filtered view to match
                SearchBox_TextChanged(null, null);
                ViewListBox.SelectedItem = selected;
            }
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            var selected = ViewListBox.SelectedItem as ViewSelectionItem;
            if (selected == null) return;

            int index = Views.IndexOf(selected);
            if (index < Views.Count - 1)
            {
                Views.Move(index, index + 1);
                // Refresh filtered view to match
                SearchBox_TextChanged(null, null);
                ViewListBox.SelectedItem = selected;
            }
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

            // Get selected version
            ACADVersion version = ACADVersion.R2018;
            if (VersionCombo.SelectedIndex == 1) version = ACADVersion.R2013;
            else if (VersionCombo.SelectedIndex == 2) version = ACADVersion.R2010;

            _handler.SelectedViewIds = selected;
            _handler.ExportPath = PathTxt.Text;
            _handler.Mode = mode;
            _handler.Version = version;
            _handler.MergeLayers = MergeLayersCheck.IsChecked ?? true;
            
            StatusText.Text = $"Exporting {selected.Count} sheets...";
            ExportProgressBar.Visibility = System.Windows.Visibility.Visible;
            ExportBtn.IsEnabled = false;

            _externalEvent.Raise();
        }

        public void UpdateStatus(string msg)
        {
            Dispatcher.Invoke(() => {
                StatusText.Text = msg;
                if (msg.Contains("Done") || msg.Contains("failed") || msg.Contains("Error"))
                {
                    ExportProgressBar.Visibility = System.Windows.Visibility.Collapsed;
                    ExportBtn.IsEnabled = true;
                }
            });
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
