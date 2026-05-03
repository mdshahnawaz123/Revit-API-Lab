using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.UI;

namespace RevitUI.UI.SharedParam
{
    public partial class SharedParamDashboard : Window
    {
        private readonly ExternalEvent _externalEvent;
        private readonly SharedParamHandler _handler;
        private List<CheckBox> _paramCheckboxes = new List<CheckBox>();
        private List<CheckBox> _categoryCheckboxes = new List<CheckBox>();
        private readonly string _historyFilePath;

        public SharedParamDashboard(ExternalEvent externalEvent, SharedParamHandler handler)
        {
            InitializeComponent();
            _externalEvent = externalEvent;
            _handler = handler;

            _historyFilePath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "B-Lab", "Revit", "SharedParamHistory.txt");

            LoadHistory();
            _externalEvent = externalEvent;
            _handler = handler;

            // Set Revit as owner to prevent native dialogs from going behind this window
            var revitProcess = System.Diagnostics.Process.GetCurrentProcess();
            new System.Windows.Interop.WindowInteropHelper(this).Owner = revitProcess.MainWindowHandle;

            this.HideIcon();

            // Populate built-in parameter groups
            CmbGroup.Items.Add("General");
            CmbGroup.Items.Add("Identity Data");
            CmbGroup.Items.Add("Dimensions");
            CmbGroup.Items.Add("Construction");
            CmbGroup.Items.Add("Graphics");
            CmbGroup.Items.Add("Phasing");
            CmbGroup.Items.Add("Mechanical");
            CmbGroup.Items.Add("Mechanical - Flow");
            CmbGroup.Items.Add("Mechanical - Loads");
            CmbGroup.Items.Add("Electrical");
            CmbGroup.Items.Add("Electrical - Lighting");
            CmbGroup.Items.Add("Electrical - Loads");
            CmbGroup.Items.Add("Plumbing");
            CmbGroup.Items.Add("Structural");
            CmbGroup.Items.Add("Structural Analysis");
            CmbGroup.Items.Add("Energy Analysis");
            CmbGroup.Items.Add("Fire Protection");
            CmbGroup.Items.Add("Materials and Finishes");
            CmbGroup.Items.Add("IFC Parameters");
            CmbGroup.Items.Add("Other");
            CmbGroup.SelectedIndex = 0;

            // Populate data types for new parameters
            PopulateDataTypes();

            // Initial load of existing project shared parameters
            _handler.Mode = SharedParamMode.FetchExisting;
            _externalEvent.Raise();
        }

        public void LoadCategories(List<string> categories)
        {
            CategoryList.Children.Clear();
            _categoryCheckboxes.Clear();

            foreach (var cat in categories.OrderBy(c => c))
            {
                var cb = new CheckBox
                {
                    Content = cat,
                    Foreground = System.Windows.Media.Brushes.White,
                    Margin = new Thickness(0, 0, 0, 6),
                    Tag = cat
                };
                _categoryCheckboxes.Add(cb);
                CategoryList.Children.Add(cb);
            }
        }

        private void LoadHistory()
        {
            try
            {
                if (System.IO.File.Exists(_historyFilePath))
                {
                    var lines = System.IO.File.ReadAllLines(_historyFilePath)
                                      .Where(l => !string.IsNullOrWhiteSpace(l) && System.IO.File.Exists(l))
                                      .Distinct()
                                      .ToList();
                    foreach (var line in lines) CmbFileHistory.Items.Add(line);
                    if (CmbFileHistory.Items.Count > 0) CmbFileHistory.SelectedIndex = 0;
                }
            }
            catch { }
        }

        private void SaveToHistory(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return;

                var items = CmbFileHistory.Items.Cast<object>().Select(i => i.ToString()).ToList();
                if (!items.Contains(path))
                {
                    CmbFileHistory.Items.Insert(0, path);
                    CmbFileHistory.SelectedIndex = 0;
                }

                var dir = System.IO.Path.GetDirectoryName(_historyFilePath);
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);

                System.IO.File.WriteAllLines(_historyFilePath, CmbFileHistory.Items.Cast<object>().Select(i => i.ToString()));
            }
            catch { }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Shared Parameter Files (*.txt)|*.txt",
                Title = "Select Shared Parameter File"
            };
            if (dlg.ShowDialog() == true)
            {
                CmbFileHistory.Text = dlg.FileName;
                BtnLoad_Click(null, null);
            }
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            string path = CmbFileHistory.Text;
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path) || path.Contains("Select a"))
            {
                MessageBox.Show(this, "Please select a valid shared parameter file first.", "B-Lab");
                return;
            }

            SaveToHistory(path);
            _handler.SharedParamFilePath = path;
            _handler.Mode = SharedParamMode.LoadFile;
            _externalEvent.Raise();

            TxtStatus.Text = "Loading parameters...";
        }

        private void TxtSearchCat_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = TxtSearchCat.Text.ToLower().Trim();
            foreach (var cb in _categoryCheckboxes)
            {
                bool matches = string.IsNullOrEmpty(filter) || cb.Content.ToString().ToLower().Contains(filter);
                cb.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cb in _categoryCheckboxes)
            {
                if (cb.Visibility == Visibility.Visible)
                    cb.IsChecked = true;
            }
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cb in _categoryCheckboxes)
            {
                if (cb.Visibility == Visibility.Visible)
                    cb.IsChecked = false;
            }
        }

        private void BtnHelp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string helpPath = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "Resources", "SharedParamHelp.html");

                if (System.IO.File.Exists(helpPath))
                    System.Diagnostics.Process.Start(helpPath);
                else
                    MessageBox.Show(this, "Help file not found.", "B-Lab");
            }
            catch { }
        }

        public void PopulateParameters(List<SharedParamInfo> parameters, bool clearExisting = true)
        {
            if (clearExisting)
            {
                ParamList.Children.Clear();
                _paramCheckboxes.Clear();
            }

            string currentGroup = "";
            foreach (var p in parameters.OrderBy(x => x.Group).ThenBy(x => x.Name))
            {
                // Avoid duplicates using Guid, but allow multiple 'N/A' GUIDs for non-shared project parameters
                if (p.Guid != "N/A" && _paramCheckboxes.Any(cb => (cb.Tag as SharedParamInfo)?.Guid == p.Guid && (cb.Tag as SharedParamInfo)?.Group == p.Group))
                    continue;
                if (p.Guid == "N/A" && _paramCheckboxes.Any(cb => (cb.Tag as SharedParamInfo)?.Name == p.Name && (cb.Tag as SharedParamInfo)?.Group == p.Group))
                    continue;

                if (p.Group != currentGroup)
                {
                    currentGroup = p.Group;
                    var header = new TextBlock
                    {
                        Text = $"▸ {currentGroup}",
                        Foreground = System.Windows.Media.Brushes.CornflowerBlue,
                        FontWeight = FontWeights.SemiBold,
                        FontSize = 12,
                        Margin = new Thickness(0, 8, 0, 4)
                    };
                    ParamList.Children.Add(header);
                }

                var cb = new CheckBox
                {
                    Margin = new Thickness(12, 0, 0, 5),
                    Tag = p
                };

                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new TextBlock { Text = p.Name, Foreground = System.Windows.Media.Brushes.White, FontSize = 12 });
                cb.Content = sp;

                _paramCheckboxes.Add(cb);
                ParamList.Children.Add(cb);
            }

            TxtStatus.Text = $"Loaded {parameters.Count} parameters from {parameters.Select(p => p.Group).Distinct().Count()} groups";
        }

        private void TxtSearchParam_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = TxtSearchParam.Text.ToLower();
            foreach (var cb in _paramCheckboxes)
            {
                if (cb.Tag is SharedParamInfo info)
                    cb.Visibility = info.Name.ToLower().Contains(query) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public List<SharedParamInfo> GetLoadedParameters()
        {
            return _paramCheckboxes.Select(cb => cb.Tag as SharedParamInfo).Where(info => info != null).ToList();
        }

        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                Title = "Export Parameters to CSV",
                FileName = "RevitSharedParameters.csv"
            };

            if (dlg.ShowDialog() == true)
            {
                _handler.CsvFilePath = dlg.FileName;
                _handler.Mode = SharedParamMode.ExportCsv;
                _externalEvent.Raise();
            }
        }

        private void BtnImportCsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                Title = "Import Parameters from CSV"
            };

            if (dlg.ShowDialog() == true)
            {
                _handler.CsvFilePath = dlg.FileName;
                _handler.Mode = SharedParamMode.ImportCsv;
                _externalEvent.Raise();
            }
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            var selectedParams = _paramCheckboxes
                .Where(cb => cb.IsChecked == true && cb.Tag is SharedParamInfo)
                .Select(cb => (SharedParamInfo)cb.Tag)
                .ToList();

            var selectedCategories = _categoryCheckboxes
                .Where(cb => cb.IsChecked == true)
                .Select(cb => cb.Tag.ToString())
                .ToList();

            if (selectedParams.Count == 0)
            {
                MessageBox.Show(this, "Please select at least one parameter.", "B-Lab");
                return;
            }
            if (selectedCategories.Count == 0)
            {
                MessageBox.Show(this, "Please select at least one category.", "B-Lab");
                return;
            }

            _handler.Mode = SharedParamMode.Apply;
            _handler.SelectedParams = selectedParams;
            _handler.SelectedCategoryNames = selectedCategories;
            _handler.IsInstance = RbInstance.IsChecked == true;
            _handler.ParameterGroupName = CmbGroup.SelectedItem?.ToString() ?? "Other";

            _externalEvent.Raise();
            TxtStatus.Text = $"Applying {selectedParams.Count} params to {selectedCategories.Count} categories...";
        }

        private void PopulateDataTypes()
        {
            CmbNewParamType.Items.Add("Text");
            CmbNewParamType.Items.Add("Multiline Text");
            CmbNewParamType.Items.Add("Integer");
            CmbNewParamType.Items.Add("Number");
            CmbNewParamType.Items.Add("Length");
            CmbNewParamType.Items.Add("Area");
            CmbNewParamType.Items.Add("Volume");
            CmbNewParamType.Items.Add("Angle");
            CmbNewParamType.Items.Add("Slope");
            CmbNewParamType.Items.Add("Currency");
            CmbNewParamType.Items.Add("URL");
            CmbNewParamType.Items.Add("Material");
            CmbNewParamType.Items.Add("Fill Pattern");
            CmbNewParamType.Items.Add("Image");
            CmbNewParamType.Items.Add("YesNo");
            CmbNewParamType.SelectedIndex = 0;
        }

        private void BtnShowCreateForm_Click(object sender, RoutedEventArgs e)
        {
            CreateParamForm.Visibility = CreateParamForm.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private void BtnCancelCreate_Click(object sender, RoutedEventArgs e)
        {
            CreateParamForm.Visibility = Visibility.Collapsed;
        }

        private void BtnCreateParam_Click(object sender, RoutedEventArgs e)
        {
            string rawNames = TxtNewParamName.Text;
            var names = rawNames.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(n => n.Trim())
                                .Where(n => !string.IsNullOrEmpty(n))
                                .ToList();
            
            string typeStr = CmbNewParamType.SelectedItem?.ToString();

            if (names.Count == 0)
            {
                MessageBox.Show(this, "Please enter at least one parameter name.", "B-Lab");
                return;
            }

            _handler.NewParamNames = names;
            _handler.NewParamTypeStr = typeStr;
            _handler.IsNewParamShared = RbNewSharedParam.IsChecked == true;
            _handler.Mode = SharedParamMode.CreateNew;
            _externalEvent.Raise();

            CreateParamForm.Visibility = Visibility.Collapsed;
            TxtNewParamName.Clear();
            TxtStatus.Text = $"Creating {names.Count} parameters...";
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
            RevitUI.Command.SharedParamCommand.Instance = null;
        }
    }

    public class SharedParamInfo
    {
        public string Name { get; set; }
        public string Group { get; set; }
        public string DataType { get; set; }
        public string Guid { get; set; }
    }
}
