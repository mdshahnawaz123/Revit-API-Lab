using System;
using System.IO;
using System.Windows;
using Autodesk.Revit.UI;

namespace RevitUI.UI.Export
{
    public partial class ExportDashboard : Window
    {
        private readonly ExternalEvent _externalEvent;
        private readonly ExportHandler _handler;

        public ExportDashboard(ExternalEvent externalEvent, ExportHandler handler)
        {
            InitializeComponent();
            this.HideIcon();
            _externalEvent = externalEvent;
            _handler = handler;

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
            _handler.NamingFormat = TxtNamingFormat.Text;

            _externalEvent.Raise();
            TaskDialog.Show("B-Lab Export", "Master Export started in background. Please wait for completion message.");
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
