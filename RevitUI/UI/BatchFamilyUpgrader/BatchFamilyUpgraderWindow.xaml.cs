using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using Autodesk.Revit.UI;
using RevitUI.ExternalCommand.BatchFamilyUpgrader;

namespace RevitUI.UI.BatchFamilyUpgrader
{
    /// <summary>
    /// Model source entry displayed in the Models ListView.
    /// </summary>
    public class ModelSourceItem : INotifyPropertyChanged
    {
        private string _status = "Pending";
        private int _familyCount;

        public string SourceType { get; set; } = "Local";
        public string Path { get; set; } = "";
        public string ProjectGuid { get; set; } = "";
        public string ModelGuid { get; set; } = "";
        public string Region { get; set; } = "US";

        public string DisplayPath => SourceType.Contains("Local")
            ? Path
            : $"Project: {ProjectGuid} | Model: {ModelGuid}";

        public int FamilyCount
        {
            get => _familyCount;
            set { _familyCount = value; OnPropertyChanged(nameof(FamilyCount)); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Family file entry for the reload feature.
    /// </summary>
    public class FamilyFileItem : INotifyPropertyChanged
    {
        private string _status = "Pending";

        /// <summary>Family name without extension (used for matching inside models).</summary>
        public string FamilyName { get; set; } = "";

        /// <summary>Full path to the .rfa file on disk.</summary>
        public string FilePath { get; set; } = "";

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// View model for cloud models discovered in the session.
    /// </summary>
    public class CloudModelDisplayItem : INotifyPropertyChanged
    {
        public string DisplayName { get; set; } = "";
        public string ProjectGuid { get; set; } = "";
        public string ModelGuid { get; set; } = "";
        public string Region { get; set; } = "US";
        public string OpenStatus => "Currently Open";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class BatchFamilyUpgraderWindow : Window
    {
        private ExternalEvent _externalEvent;
        private BatchFamilyUpgraderHandler _handler;
        private ExternalEvent _fetchEvent;
        private CloudModelFetchHandler _fetchHandler;

        private ObservableCollection<ModelSourceItem> _modelSources;
        private ObservableCollection<FamilyFileItem> _familyFiles;

        // Temp storage for the file browse dialog result
        private string[] _pendingFamilyFilePaths;
        private string[] _pendingCloudDCPaths;

        public BatchFamilyUpgraderWindow(ExternalEvent externalEvent, BatchFamilyUpgraderHandler handler, 
                                        ExternalEvent fetchEvent, CloudModelFetchHandler fetchHandler)
        {
            InitializeComponent();
            _externalEvent = externalEvent;
            _handler = handler;
            _handler.Dashboard = this;

            _fetchEvent = fetchEvent;
            _fetchHandler = fetchHandler;
            _fetchHandler.OnCloudModelsFetched = HandleCloudModelsFetched;

            _modelSources = new ObservableCollection<ModelSourceItem>();
            lvModels.ItemsSource = _modelSources;

            _familyFiles = new ObservableCollection<FamilyFileItem>();
            lvFamilyFiles.ItemsSource = _familyFiles;


            LogMessage("Batch Family Upgrader initialized. Add model sources to begin.");
            LogMessage("Tip: Add .rfa files in the Family Reload section to update families inside models.");
        }

        private void HandleCloudModelsFetched(string loginUser, List<CloudModelInfo> models)
        {
            // Legacy cloud fetch logic no longer updates UI. 
            // We now use Desktop Connector.
        }

        /// <summary>
        /// Returns the full list of model sources for the handler to process.
        /// </summary>
        public List<ModelSourceItem> GetModelSources() => _modelSources.ToList();

        /// <summary>
        /// Returns the list of family files to reload into target models.
        /// </summary>
        public List<FamilyFileItem> GetFamilyFiles() => _familyFiles.ToList();

        /// <summary>
        /// Whether the user wants to overwrite existing parameter values when reloading families.
        /// </summary>
        public bool OverwriteParameterValues => chkOverwriteParams.IsChecked == true;

        // ═══════════════════════════════════════════
        //  SOURCE TYPE TOGGLE
        // ═══════════════════════════════════════════

        private void SourceType_Changed(object sender, RoutedEventArgs e)
        {
            if (panelLocal == null || panelCloud == null) return;

            bool isLocal = rbLocal.IsChecked == true;
            panelLocal.Visibility = isLocal ? Visibility.Visible : Visibility.Collapsed;
            panelCloud.Visibility = isLocal ? Visibility.Collapsed : Visibility.Visible;
        }

        // ═══════════════════════════════════════════
        //  BROWSE LOCAL FOLDER
        // ═══════════════════════════════════════════

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select the folder containing Revit Families (.rfa)";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    txtFolderPath.Text = dialog.SelectedPath;
                    txtFolderPath.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#ECEFF4"));
                }
            }
        }

        // ═══════════════════════════════════════════
        //  ADD SOURCE (Local / Cloud)
        // ═══════════════════════════════════════════

        private void BtnAddSource_Click(object sender, RoutedEventArgs e)
        {
            if (rbLocal.IsChecked == true)
            {
                AddLocalSource();
            }
        }

        private void AddLocalSource()
        {
            string folderPath = txtFolderPath.Text?.Trim();
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            {
                System.Windows.MessageBox.Show("Please browse and select a valid folder first.",
                    "Invalid Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check for duplicates
            if (_modelSources.Any(m => m.SourceType.Contains("Local") && m.Path == folderPath))
            {
                System.Windows.MessageBox.Show("This folder is already added.",
                    "Duplicate", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int fileCount = Directory.GetFiles(folderPath, "*.rfa", SearchOption.AllDirectories).Length;

            var item = new ModelSourceItem
            {
                SourceType = "📁 Local",
                Path = folderPath,
                FamilyCount = fileCount,
                Status = "Ready"
            };
            _modelSources.Add(item);
            UpdateModelCount();

            LogMessage($"Added local folder: {folderPath} ({fileCount} families found)");

            // Reset input
            txtFolderPath.Text = "Select a folder containing .rfa files...";
            txtFolderPath.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#5A6478"));
        }



        // ═══════════════════════════════════════════
        //  CLOUD BROWSER ACTIONS (Desktop Connector)
        // ═══════════════════════════════════════════

        private void BtnBrowseCloud_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Target Cloud Models via Desktop Connector",
                Filter = "Revit Models (*.rvt)|*.rvt",
                Multiselect = true
            };

            // Attempt to default to Autodesk Docs folder if it exists
            string dcPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "DC", "ACCDocs");
            if (Directory.Exists(dcPath))
            {
                dialog.InitialDirectory = dcPath;
            }

            if (dialog.ShowDialog() == true)
            {
                _pendingCloudDCPaths = dialog.FileNames;
                int count = _pendingCloudDCPaths.Length;
                txtCloudDCPath.Text = count == 1
                    ? System.IO.Path.GetFileName(_pendingCloudDCPaths[0])
                    : $"{count} cloud models selected";
                txtCloudDCPath.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#ECEFF4"));
            }
        }

        private void BtnAddCloudDC_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingCloudDCPaths == null || _pendingCloudDCPaths.Length == 0)
            {
                System.Windows.MessageBox.Show("Please browse and select cloud .rvt files first.",
                    "No Files", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int addedCount = 0;
            foreach (string filePath in _pendingCloudDCPaths)
            {
                // Check for duplicates
                if (_modelSources.Any(m => m.SourceType.Contains("Cloud") && m.Path.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                {
                    LogMessage($"  ⚠ Skipped duplicate cloud model: {Path.GetFileName(filePath)}");
                    continue;
                }

                _modelSources.Add(new ModelSourceItem
                {
                    SourceType = "☁ Cloud",
                    Path = filePath,
                    FamilyCount = 0, // Will be resolved during upgrade
                    Status = "Ready"
                });
                addedCount++;
            }

            UpdateModelCount();
            LogMessage($"Added {addedCount} cloud model(s) via Desktop Connector.");

            // Reset
            _pendingCloudDCPaths = null;
            txtCloudDCPath.Text = "Select target .rvt files from ACC...";
            txtCloudDCPath.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#5A6478"));
        }

        // ═══════════════════════════════════════════
        //  BROWSE & ADD FAMILY FILES (.rfa)
        // ═══════════════════════════════════════════

        private void BtnBrowseFamilies_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Revit Family Files to Reload",
                Filter = "Revit Family Files (*.rfa)|*.rfa",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                _pendingFamilyFilePaths = dialog.FileNames;
                int count = _pendingFamilyFilePaths.Length;
                txtFamilyFiles.Text = count == 1
                    ? System.IO.Path.GetFileName(_pendingFamilyFilePaths[0])
                    : $"{count} family files selected";
                txtFamilyFiles.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#ECEFF4"));
            }
        }

        private void BtnAddFamilies_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingFamilyFilePaths == null || _pendingFamilyFilePaths.Length == 0)
            {
                System.Windows.MessageBox.Show("Please browse and select .rfa files first.",
                    "No Files", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int addedCount = 0;
            foreach (string filePath in _pendingFamilyFilePaths)
            {
                string familyName = System.IO.Path.GetFileNameWithoutExtension(filePath);

                // Check for duplicates
                if (_familyFiles.Any(f => f.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                {
                    LogMessage($"  ⚠ Skipped duplicate: {familyName}");
                    continue;
                }

                _familyFiles.Add(new FamilyFileItem
                {
                    FamilyName = familyName,
                    FilePath = filePath,
                    Status = "Ready"
                });
                addedCount++;
            }

            UpdateFamilyFileCount();
            LogMessage($"Added {addedCount} family file(s) for reload.");

            // Reset
            _pendingFamilyFilePaths = null;
            txtFamilyFiles.Text = "Select .rfa family files to reload...";
            txtFamilyFiles.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#5A6478"));
        }

        private void BtnRemoveFamilyFiles_Click(object sender, RoutedEventArgs e)
        {
            var selected = lvFamilyFiles.SelectedItems.Cast<FamilyFileItem>().ToList();
            if (selected.Count == 0)
            {
                System.Windows.MessageBox.Show("Please select one or more family files to remove.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var item in selected)
            {
                _familyFiles.Remove(item);
                LogMessage($"Removed family file: {item.FamilyName}");
            }
            UpdateFamilyFileCount();
        }

        // ═══════════════════════════════════════════
        //  REMOVE / CLEAR
        // ═══════════════════════════════════════════

        private void BtnRemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = lvModels.SelectedItems.Cast<ModelSourceItem>().ToList();
            if (selected.Count == 0)
            {
                System.Windows.MessageBox.Show("Please select one or more model sources to remove.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var item in selected)
            {
                _modelSources.Remove(item);
                LogMessage($"Removed: {(item.SourceType.Contains("Local") ? item.Path : item.ModelGuid)}");
            }
            UpdateModelCount();
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (_modelSources.Count == 0 && _familyFiles.Count == 0) return;

            var result = System.Windows.MessageBox.Show(
                "Remove all model sources and family files?", "Confirm Clear",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _modelSources.Clear();
                _familyFiles.Clear();
                UpdateModelCount();
                UpdateFamilyFileCount();
                LogMessage("All model sources and family files cleared.");
            }
        }

        // ═══════════════════════════════════════════
        //  START UPGRADE
        // ═══════════════════════════════════════════

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (_modelSources.Count == 0)
            {
                System.Windows.MessageBox.Show("Please add at least one model source before starting.",
                    "No Sources", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Pass all data to handler
            _handler.ModelSources = GetModelSources();
            _handler.FamilyFilesToReload = GetFamilyFiles();
            _handler.OverwriteParameterValues = OverwriteParameterValues;

            // Disable controls during processing
            SetControlsEnabled(false);
            pbProgress.Value = 0;
            txtPercentage.Text = "0%";

            LogMessage("═══════════════════════════════════════");
            if (_familyFiles.Count > 0)
            {
                LogMessage($"Starting batch upgrade + family reload ({_familyFiles.Count} families to reload)...");
            }
            else
            {
                LogMessage("Starting batch upgrade across all sources...");
            }

            _externalEvent.Raise();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // ═══════════════════════════════════════════
        //  PROGRESS & LOG (called by Handler)
        // ═══════════════════════════════════════════

        public void UpdateProgress(int current, int total, string fileName)
        {
            pbProgress.Maximum = total;
            pbProgress.Value = current;
            int percent = total > 0 ? (int)((double)current / total * 100) : 0;
            txtPercentage.Text = $"{percent}%";
            txtProgress.Text = $"Processing: {current} / {total}";
            LogMessage($"  ✓ {fileName}");
        }

        public void UpdateSourceStatus(int sourceIndex, string status)
        {
            if (sourceIndex >= 0 && sourceIndex < _modelSources.Count)
            {
                _modelSources[sourceIndex].Status = status;
            }
        }

        public void LogMessage(string message)
        {
            lstLogs.Items.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            lstLogs.ScrollIntoView(lstLogs.Items[lstLogs.Items.Count - 1]);
        }

        public void UpgradeComplete(int successCount, int totalCount)
        {
            SetControlsEnabled(true);

            pbProgress.Value = pbProgress.Maximum;
            txtPercentage.Text = "100%";
            txtProgress.Text = "Complete!";

            LogMessage("═══════════════════════════════════════");
            LogMessage($"🎉 Upgrade finished: {successCount} of {totalCount} operations completed successfully.");

            System.Windows.MessageBox.Show(
                $"Upgrade finished!\n\nSuccess: {successCount}\nTotal: {totalCount}",
                "Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ═══════════════════════════════════════════
        //  PLACEHOLDER TEXT HELPERS
        // ═══════════════════════════════════════════



        // ═══════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════

        private void UpdateModelCount()
        {
            txtModelCount.Text = $"  ({_modelSources.Count} source{(_modelSources.Count != 1 ? "s" : "")})";
        }

        private void UpdateFamilyFileCount()
        {
            txtFamilyFileCount.Text = $"  ({_familyFiles.Count} file{(_familyFiles.Count != 1 ? "s" : "")})";
        }

        private void SetControlsEnabled(bool enabled)
        {
            btnStart.IsEnabled = enabled;
            btnBrowse.IsEnabled = enabled;
            btnAddLocal.IsEnabled = enabled;
            btnRemoveSelected.IsEnabled = enabled;
            btnClearAll.IsEnabled = enabled;
            btnBrowseFamilies.IsEnabled = enabled;
            btnAddFamilies.IsEnabled = enabled;
            btnRemoveFamilyFiles.IsEnabled = enabled;
            
            btnBrowseCloud.IsEnabled = enabled;
            btnAddCloudDC.IsEnabled = enabled;
        }
    }
}
