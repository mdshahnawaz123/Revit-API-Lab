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

        public SharedParamDashboard(ExternalEvent externalEvent, SharedParamHandler handler)
        {
            InitializeComponent();
            this.HideIcon();
            _externalEvent = externalEvent;
            _handler = handler;

            // Populate built-in parameter groups
            CmbGroup.Items.Add("Identity Data");
            CmbGroup.Items.Add("Dimensions");
            CmbGroup.Items.Add("Construction");
            CmbGroup.Items.Add("Structural");
            CmbGroup.Items.Add("Mechanical");
            CmbGroup.Items.Add("Electrical");
            CmbGroup.Items.Add("Plumbing");
            CmbGroup.Items.Add("Energy Analysis");
            CmbGroup.Items.Add("Other");
            CmbGroup.SelectedIndex = 0;
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

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Shared Parameter Files (*.txt)|*.txt",
                Title = "Select Shared Parameter File"
            };
            if (dlg.ShowDialog() == true)
            {
                TxtFilePath.Text = dlg.FileName;
                TxtFilePath.Foreground = System.Windows.Media.Brushes.White;
            }
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtFilePath.Text) || TxtFilePath.Text.Contains("Select a"))
            {
                MessageBox.Show("Please select a shared parameter file first.", "B-Lab");
                return;
            }

            _handler.SharedParamFilePath = TxtFilePath.Text;
            _handler.Mode = SharedParamMode.LoadFile;
            _externalEvent.Raise();

            TxtStatus.Text = "Loading parameters...";
        }

        public void PopulateParameters(List<SharedParamInfo> parameters)
        {
            ParamList.Children.Clear();
            _paramCheckboxes.Clear();

            string currentGroup = "";
            foreach (var p in parameters.OrderBy(x => x.Group).ThenBy(x => x.Name))
            {
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
                sp.Children.Add(new TextBlock
                {
                    Text = $"  ({p.DataType})",
                    Foreground = System.Windows.Media.Brushes.Gray,
                    FontSize = 10,
                    VerticalAlignment = VerticalAlignment.Center
                });
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

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cb in _categoryCheckboxes) cb.IsChecked = true;
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cb in _categoryCheckboxes) cb.IsChecked = false;
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
                MessageBox.Show("Please select at least one parameter.", "B-Lab");
                return;
            }
            if (selectedCategories.Count == 0)
            {
                MessageBox.Show("Please select at least one category.", "B-Lab");
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
