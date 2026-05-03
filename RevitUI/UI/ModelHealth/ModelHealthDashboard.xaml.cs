using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Autodesk.Revit.UI;
using RevitUI.Command;
using RevitUI.ExternalCommand.ModelHealth;
using RevitUI.UI;

namespace RevitUI.UI.ModelHealth
{
    public partial class ModelHealthDashboard : Window
    {
        private readonly ExternalEvent _externalEvent;
        private readonly ModelHealthHandler _handler;
        private ModelHealthData _lastData;

        public ModelHealthDashboard(ExternalEvent externalEvent, ModelHealthHandler handler)
        {
            InitializeComponent();
            this.HideIcon();
            _externalEvent = externalEvent;
            _handler = handler;
            
            // Trigger initial scan
            _externalEvent.Raise();
        }

        public void UpdateMetrics(ModelHealthData data)
        {
            _lastData = data;
            ScoreText.Text = ((int)data.HealthScore).ToString();
            WarningCount.Text = data.WarningCount.ToString();
            InPlaceCount.Text = data.InPlaceCount.ToString();
            GroupCount.Text = data.GroupCount.ToString();
            CadCount.Text = data.CadImportCount.ToString();
            RoomCount.Text = data.RedundantRoomCount.ToString();
            ViewCount.Text = data.OrphanedViewCount.ToString();
            LinkCount.Text = data.LinkCount.ToString();
            FilterCount.Text = data.UnusedFilterCount.ToString();

            // Advanced Data
            FileSizeText.Text = $"{data.FileSizeMb} MB";
            WorksetText.Text = data.WorksetCount.ToString();
            DesignText.Text = data.DesignOptionCount.ToString();
            ImageText.Text = data.ImageCount.ToString();
            MaterialText.Text = data.MaterialCount.ToString();
            StyleText.Text = data.LineStyleCount.ToString();
            ViewTemplateText.Text = data.ViewWithoutTemplateCount.ToString();
            TotalElementsText.Text = data.TotalElementCount.ToString();

            // Update Status color and text
            if (data.HealthScore > 80)
            {
                HealthStatus.Text = "Excellent";
                HealthStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129)); // Green
            }
            else if (data.HealthScore > 50)
            {
                HealthStatus.Text = "Attention Needed";
                HealthStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)); // Amber
            }
            else
            {
                HealthStatus.Text = "Critical";
                HealthStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)); // Red
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (_lastData == null) return;

            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.Filter = "CSV file (*.csv)|*.csv";
            saveFileDialog.FileName = "ModelHealth_Report.csv";
            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    var lines = new List<string>
                    {
                        "Category,Metric,Value",
                        $"Summary,Health Score,{_lastData.HealthScore}",
                        $"Performance,Warnings,{_lastData.WarningCount}",
                        $"Performance,File Size MB,{_lastData.FileSizeMb}",
                        $"Elements,Total Elements,{_lastData.TotalElementCount}",
                        $"Elements,In-Place Families,{_lastData.InPlaceCount}",
                        $"Elements,Groups,{_lastData.GroupCount}",
                        $"External,CAD Imports,{_lastData.CadImportCount}",
                        $"External,Links,{_lastData.LinkCount}",
                        $"Project,Worksets,{_lastData.WorksetCount}",
                        $"Project,Design Options,{_lastData.DesignOptionCount}",
                        $"QC,Redundant Rooms,{_lastData.RedundantRoomCount}",
                        $"QC,Orphaned Views,{_lastData.OrphanedViewCount}",
                        $"QC,Unused Filters,{_lastData.UnusedFilterCount}",
                        $"QC,Views w/o Templates,{_lastData.ViewWithoutTemplateCount}"
                    };
                    System.IO.File.WriteAllLines(saveFileDialog.FileName, lines);
                    TaskDialog.Show("B-Lab", "Report exported successfully! You can now load this into your PowerBI template.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error exporting: " + ex.Message);
                }
            }
        }

        private void ExportHtml_Click(object sender, RoutedEventArgs e)
        {
            if (_lastData == null) return;

            // Ask for Company Name
            var inputDlg = new CompanyInputDialog();
            inputDlg.Owner = this;
            if (inputDlg.ShowDialog() != true) return;
            string companyName = inputDlg.CompanyName;

            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.Filter = "HTML file (*.html)|*.html";
            saveFileDialog.FileName = $"{companyName}_ModelHealth.html";
            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    string status = _lastData.HealthScore > 80 ? "Excellent" : (_lastData.HealthScore > 50 ? "Attention Needed" : "Critical");
                    string color = _lastData.HealthScore > 80 ? "#10b981" : (_lastData.HealthScore > 50 ? "#f59e0b" : "#ef4444");
                    
                    double warningsPerMb = 0;
                    if (double.TryParse(_lastData.FileSizeMb, out double fs) && fs > 0)
                        warningsPerMb = _lastData.WarningCount / fs;

                    string html = $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <title>{companyName} - Model Integrity Dashboard</title>
    <style>
        :root {{ --primary: #2563eb; --success: #10b981; --warning: #f59e0b; --danger: #ef4444; --gray: #6b7280; --light: #f3f4f6; }}
        body {{ font-family: 'Segoe UI', system-ui, sans-serif; background: #f9fafb; margin: 0; padding: 40px; color: #111827; }}
        .container {{ max-width: 1200px; margin: 0 auto; }}
        .header {{ display: flex; justify-content: space-between; align-items: baseline; border-bottom: 2px solid #e5e7eb; padding-bottom: 10px; margin-bottom: 30px; }}
        
        /* GAUGES */
        .gauges-row {{ display: grid; grid-template-columns: repeat(4, 1fr); gap: 20px; margin-bottom: 40px; text-align: center; }}
        .gauge-card {{ background: white; padding: 20px; border-radius: 12px; border: 1px solid #e5e7eb; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }}
        .gauge-svg {{ width: 100px; height: 60px; }}
        .gauge-val {{ font-size: 24px; font-weight: bold; margin-top: 5px; }}
        .gauge-label {{ font-size: 12px; color: var(--gray); font-weight: 500; }}

        /* METRIC GROUPS */
        .grid {{ display: grid; grid-template-columns: repeat(2, 1fr); gap: 30px; }}
        .group-box {{ background: #fff; border-radius: 12px; border: 1px solid #e5e7eb; overflow: hidden; }}
        .group-header {{ background: #f8fafc; padding: 12px 20px; border-bottom: 1px solid #e5e7eb; font-weight: bold; display: flex; align-items: center; }}
        .group-header i {{ margin-right: 10px; }}
        .cards-grid {{ display: grid; grid-template-columns: repeat(2, 1fr); gap: 10px; padding: 15px; }}
        .card {{ padding: 15px; border-radius: 8px; color: white; position: relative; }}
        .card.red {{ background: var(--danger); }}
        .card.orange {{ background: var(--warning); }}
        .card.green {{ background: var(--success); }}
        .card.gray {{ background: #9ca3af; }}
        .card-val {{ font-size: 22px; font-weight: bold; }}
        .card-label {{ font-size: 11px; opacity: 0.9; margin-top: 4px; }}

        /* DETAILS SECTION */
        .details-section {{ margin-top: 50px; background: white; border-radius: 12px; border: 1px solid #e5e7eb; padding: 20px; }}
        details {{ margin-bottom: 10px; border-bottom: 1px solid #f3f4f6; padding-bottom: 10px; }}
        summary {{ cursor: pointer; font-weight: 600; padding: 10px; border-radius: 6px; }}
        summary:hover {{ background: #f9fafb; }}
        .detail-list {{ list-style: none; padding: 10px 20px; margin: 0; font-size: 13px; color: var(--gray); max-height: 200px; overflow-y: auto; }}
        
        footer {{ margin-top: 60px; text-align: center; color: var(--gray); font-size: 12px; border-top: 1px solid #e5e7eb; padding-top: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>{companyName} - Model Integrity Dashboard</h1>
            <p>{DateTime.Now:D}</p>
        </div>

        <div class='gauges-row'>
            <div class='gauge-card'>
                <svg viewBox='0 0 100 60' class='gauge-svg'><path d='M10 50 A40 40 0 0 1 90 50' fill='none' stroke='#e5e7eb' stroke-width='8'/><path d='M10 50 A40 40 0 0 1 90 50' fill='none' stroke='var(--danger)' stroke-width='8' stroke-dasharray='126' stroke-dashoffset='40'/></svg>
                <div class='gauge-val'>{warningsPerMb:F2}</div>
                <div class='gauge-label'>Warnings Per MB</div>
            </div>
            <div class='gauge-card'>
                <svg viewBox='0 0 100 60' class='gauge-svg'><path d='M10 50 A40 40 0 0 1 90 50' fill='none' stroke='#e5e7eb' stroke-width='8'/><path d='M10 50 A40 40 0 0 1 90 50' fill='none' stroke='var(--warning)' stroke-width='8' stroke-dasharray='126' stroke-dashoffset='60'/></svg>
                <div class='gauge-val'>{_lastData.FileSizeMb}</div>
                <div class='gauge-label'>File Size in MB</div>
            </div>
            <div class='gauge-card'>
                <svg viewBox='0 0 100 60' class='gauge-svg'><path d='M10 50 A40 40 0 0 1 90 50' fill='none' stroke='#e5e7eb' stroke-width='8'/><path d='M10 50 A40 40 0 0 1 90 50' fill='none' stroke='var(--success)' stroke-width='8' stroke-dasharray='126' stroke-dashoffset='20'/></svg>
                <div class='gauge-val'>{(double)_lastData.TotalElementCount / 1000:F1}K</div>
                <div class='gauge-label'>Total Placed Elements</div>
            </div>
            <div class='gauge-card'>
                <svg viewBox='0 0 100 60' class='gauge-svg'><path d='M10 50 A40 40 0 0 1 90 50' fill='none' stroke='#e5e7eb' stroke-width='8'/><path d='M10 50 A40 40 0 0 1 90 50' fill='none' stroke='var(--primary)' stroke-width='8' stroke-dasharray='126' stroke-dashoffset='80'/></svg>
                <div class='gauge-val'>{_lastData.WarningCount}</div>
                <div class='gauge-label'>Total Warnings</div>
            </div>
        </div>

        <div class='grid'>
            <div class='group-box'>
                <div class='group-header'>Elements Performance</div>
                <div class='cards-grid'>
                    <div class='card red'><div class='card-val'>{_lastData.InPlaceCount}</div><div class='card-label'>In-Place Families</div></div>
                    <div class='card orange'><div class='card-val'>{_lastData.GroupCount}</div><div class='card-label'>Model Groups</div></div>
                    <div class='card gray'><div class='card-val'>{_lastData.MaterialCount}</div><div class='card-label'>Total Materials</div></div>
                    <div class='card green'><div class='card-val'>{_lastData.LineStyleCount}</div><div class='card-label'>Line Styles</div></div>
                </div>
            </div>

            <div class='group-box'>
                <div class='group-header'>Views</div>
                <div class='cards-grid'>
                    <div class='card green'><div class='card-val'>{_lastData.OrphanedViewCount}</div><div class='card-label'>Not On Sheets</div></div>
                    <div class='card orange'><div class='card-val'>{_lastData.ViewWithoutTemplateCount}</div><div class='card-label'>w/o Templates</div></div>
                    <div class='card red'><div class='card-val'>{_lastData.UnusedFilterCount}</div><div class='card-label'>Unused Filters</div></div>
                    <div class='card gray'><div class='card-val'>{_lastData.RedundantRoomCount}</div><div class='card-label'>Redundant Rooms</div></div>
                </div>
            </div>

            <div class='group-box'>
                <div class='group-header'>Imports & Links</div>
                <div class='cards-grid'>
                    <div class='card red'><div class='card-val'>{_lastData.CadImportCount}</div><div class='card-label'>CAD Imports</div></div>
                    <div class='card orange'><div class='card-val'>{_lastData.LinkCount}</div><div class='card-label'>Revit Links</div></div>
                    <div class='card green'><div class='card-val'>{_lastData.ImageCount}</div><div class='card-label'>Raster Images</div></div>
                    <div class='card green'><div class='card-val'>{_lastData.WorksetCount}</div><div class='card-label'>Worksets</div></div>
                </div>
            </div>
            
            <div class='group-box'>
                <div class='group-header'>Worksets & Options</div>
                <div class='cards-grid'>
                    <div class='card orange'><div class='card-val'>{_lastData.WorksetCount}</div><div class='card-label'>Total Worksets</div></div>
                    <div class='card red'><div class='card-val'>{_lastData.DesignOptionCount}</div><div class='card-label'>Design Options</div></div>
                </div>
            </div>
        </div>

        <div class='details-section'>
            <h2 style='margin-top:0'>Audit Drill-down Details</h2>
            <details>
                <summary>In-Place Families ({_lastData.InPlaceCount})</summary>
                <ul class='detail-list'>{string.Join("", _lastData.InPlaceNames.Select(n => $"<li>{n}</li>"))}</ul>
            </details>
            <details>
                <summary>CAD Imports ({_lastData.CadImportCount})</summary>
                <ul class='detail-list'>{string.Join("", _lastData.CadNames.Select(n => $"<li>{n}</li>"))}</ul>
            </details>
            <details>
                <summary>Model Groups ({_lastData.GroupCount})</summary>
                <ul class='detail-list'>{string.Join("", _lastData.GroupNames.Select(n => $"<li>{n}</li>"))}</ul>
            </details>
            <details>
                <summary>Linked Models ({_lastData.LinkCount})</summary>
                <ul class='detail-list'>{string.Join("", _lastData.LinkNames.Select(n => $"<li>{n}</li>"))}</ul>
            </details>
            <details>
                <summary>Views Not On Sheets ({_lastData.OrphanedViewCount})</summary>
                <ul class='detail-list'>{string.Join("", _lastData.OrphanedViewNames.Select(n => $"<li>{n}</li>"))}</ul>
            </details>
        </div>

        <footer>
            <p>B-Lab Revit Suite - Professional BIM Audit Intelligence Dashboard</p>
            <p>© {DateTime.Now.Year} BIM Digital Design</p>
        </footer>
    </div>
</body>
</html>";
                    System.IO.File.WriteAllText(saveFileDialog.FileName, html);
                    TaskDialog.Show("B-Lab", "Professional Dashboard HTML Report generated successfully!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error generating HTML: " + ex.Message);
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            _externalEvent.Raise();
        }
    }
}
