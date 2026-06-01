using System;
using System.IO;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using RevitUI.UI;

namespace RevitUI.UI.Export
{
    public partial class ExportDashboard : Window
    {
        private readonly ExternalEvent _externalEvent;
        private readonly ExportHandler _handler;

        public ObservableCollection<ExportHandler.ViewSelectionItem> Sheets { get; set; }
        private ObservableCollection<ExportHandler.ViewSelectionItem> _filteredSheets;
        private Document _doc;

        public ExportDashboard(Document doc, ExternalEvent externalEvent, ExportHandler handler)
        {
            InitializeComponent();
            _doc = doc;
            _externalEvent = externalEvent;
            _handler = handler;

            // Load Sheets
            var sheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .OrderBy(s => s.SheetNumber)
                .Select(s => new ExportHandler.ViewSelectionItem { Name = $"{s.SheetNumber} - {s.Name}", Id = s.Id, IsChecked = false })
                .ToList();

            Sheets = new ObservableCollection<ExportHandler.ViewSelectionItem>(sheets);
            _filteredSheets = new ObservableCollection<ExportHandler.ViewSelectionItem>(sheets);
            SheetListBox.ItemsSource = _filteredSheets;

            // Default path
            TxtPath.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "B-Lab Exports");
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    TxtPath.Text = dialog.SelectedPath;
                }
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            _handler.ExportPath = TxtPath.Text;
            _handler.ExportPdf = ChkPdf.IsChecked == true;
            _handler.ExportCad = ChkCad.IsChecked == true;
            _handler.ExportNwc = ChkNwc.IsChecked == true;
            _handler.CombinePdf = ChkCombinePdf.IsChecked == true;
            _handler.ExportLinksIndividually = ChkLinksIndividually.IsChecked == true;
            _handler.ExportNwcByLevel = ChkNwcByLevel.IsChecked == true;
            _handler.NamingFormat = TxtNamingFormat.Text;

            // Handle Range
            if (RadioCurrent.IsChecked == true)
            {
                _handler.SelectedViewIds = new List<Autodesk.Revit.DB.ElementId> { _doc.ActiveView.Id };
            }
            else if (RadioAll.IsChecked == true)
            {
                _handler.SelectedViewIds = Sheets.Select(s => s.Id).ToList();
            }
            else if (RadioSelected.IsChecked == true)
            {
                _handler.SelectedViewIds = Sheets.Where(s => s.IsChecked).Select(s => s.Id).ToList();
                if (_handler.SelectedViewIds.Count == 0)
                {
                    TaskDialog.Show("B-Lab", "Please select at least one sheet.");
                    return;
                }
            }

            _externalEvent.Raise();
            TaskDialog.Show("B-Lab Export", "Master Export started in background. Please wait for completion message.");
        }

        private void Radio_Changed(object sender, RoutedEventArgs e)
        {
            if (SelectionGrid == null) return;
            SelectionGrid.Visibility = (RadioSelected.IsChecked == true) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = SearchBox.Text.ToLower();
            if (string.IsNullOrWhiteSpace(filter))
            {
                _filteredSheets = new ObservableCollection<ExportHandler.ViewSelectionItem>(Sheets);
            }
            else
            {
                var filtered = Sheets.Where(s => s.Name.ToLower().Contains(filter)).ToList();
                _filteredSheets = new ObservableCollection<ExportHandler.ViewSelectionItem>(filtered);
            }
            SheetListBox.ItemsSource = _filteredSheets;
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
            RevitUI.Command.AutoCadExportCommand.Instance = null;
        }
    }
}
