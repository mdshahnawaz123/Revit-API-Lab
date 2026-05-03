using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitUI.ExternalCommand.ParameterFilter;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using static RevitUI.ExternalCommand.ParameterFilter.SmartFilterColorHandler;

namespace RevitUI.UI
{
    public partial class ParamFilter : Window
    {
        // ✅ Single static instance
        private static ParamFilter? _instance;
        public static ParamFilter? Instance => _instance;

        private readonly Document _doc;
        private readonly UIDocument _uidoc;

        private readonly ExternalEvent _applyFilterEvent;
        private readonly ParamExternal _paramExternal;

        private readonly ExternalEvent _IsolateEvent;
        private readonly IsolateExternal _IsoExternal;

        private List<Parameter> _currentParameters = new();

        private System.Windows.Media.Color _selectedColor = Colors.White;

        private readonly AssignColorHandler _colorHandler;
        private readonly ExternalEvent _colorEvent;

        private readonly ClearColorHandler _clearHandler;
        private readonly ExternalEvent _clearEvent;

        private readonly SmartFilterColorHandler _smartHandler;
        private readonly ExternalEvent _smartEvent;



        [DllImport("user32.dll")]
        static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        const int WM_SETICON = 0x80;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hwnd = new WindowInteropHelper(this).Handle;

            // Remove small & large icon
            SendMessage(hwnd, WM_SETICON, 0, 0);
            SendMessage(hwnd, WM_SETICON, 1, 0);
        }

        // ✅ Singleton - assign _instance BEFORE Show()
        public static void GetOrCreate(Document doc, UIDocument uidoc)
        {
            if (_instance != null)
            {
                if (!_instance.IsVisible)
                    _instance.Show();

                if (_instance.WindowState == WindowState.Minimized)
                    _instance.WindowState = WindowState.Normal;

                _instance.Activate();
                _instance.Focus();
                return;
            }

            // ✅ Assign FIRST - any re-entrant call will now see _instance != null
            _instance = new ParamFilter(doc, uidoc);
            _instance.Closed += (s, e) => _instance = null;
            _instance.Show(); // Show AFTER assignment
        }

        // ✅ Private constructor - cannot use "new" from outside
        private ParamFilter(Document doc, UIDocument uidoc)
        {
            InitializeComponent();

            _doc = doc;
            _uidoc = uidoc;

            _paramExternal = new ParamExternal();
            _applyFilterEvent = ExternalEvent.Create(_paramExternal);

            _IsoExternal = new IsolateExternal();
            _IsolateEvent = ExternalEvent.Create(_IsoExternal);

            _colorHandler = new AssignColorHandler();
            _colorEvent = ExternalEvent.Create(_colorHandler);

            _clearHandler = new ClearColorHandler();
            _clearEvent = ExternalEvent.Create(_clearHandler);

            _smartHandler = new SmartFilterColorHandler();
            _smartEvent = ExternalEvent.Create(_smartHandler);

            LoadCategories();
        }

        private void LoadCategories()
        {
            var categories = _doc.Settings.Categories
                .Cast<Category>()
                .Where(c => c.CategoryType == CategoryType.Model && c.AllowsBoundParameters)
                .OrderBy(c => c.Name)
                .ToList();

            CategoryCombo.ItemsSource = categories;
            CategoryCombo.DisplayMemberPath = "Name";
        }

        private void ElementWhenCategoryChanged(object sender, SelectionChangedEventArgs e)
        {
            var cat = CategoryCombo.SelectedItem as Category;
            if (cat == null) return;

            _currentParameters = new List<Parameter>();

            var instanceElement = new FilteredElementCollector(_doc)
                .OfCategoryId(cat.Id)
                .WhereElementIsNotElementType()
                .FirstOrDefault();

            if (instanceElement != null)
                _currentParameters.AddRange(instanceElement.Parameters.Cast<Parameter>());

            var typeElement = new FilteredElementCollector(_doc)
                .OfCategoryId(cat.Id)
                .WhereElementIsElementType()
                .FirstOrDefault();

            if (typeElement != null)
                _currentParameters.AddRange(typeElement.Parameters.Cast<Parameter>());

            _currentParameters = _currentParameters
                .Where(p => p.Definition != null)
                .GroupBy(p => p.Definition.Name)
                .Select(g => g.First())
                .OrderBy(p => p.Definition.Name)
                .ToList();

            ParameterCombo.DisplayMemberPath = "Definition.Name";
            ParameterCombo.ItemsSource = _currentParameters;
        }

        private void OnApplyFilter(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs(out Category selectedCat, out Parameter selectedParam,
                out string selectedRule, out string value))
                return;

            _paramExternal.CategoryId = selectedCat.Id;
            _paramExternal.ParameterElementId = selectedParam.Id;
            _paramExternal.RuleOperator = selectedRule;
            _paramExternal.FilterValue = value;

            _paramExternal.ScopeMode = GetScopeMode();

            _applyFilterEvent.Raise();
        }

        private void OnIsolate(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs(out Category selectedCat, out Parameter selectedParam,
                out string selectedRule, out string value))
                return;

            _IsoExternal.CategoryId = selectedCat.Id;
            _IsoExternal.ParameterElementId = selectedParam.Id;
            _IsoExternal.RuleOperator = selectedRule;
            _IsoExternal.FilterValue = value;

            _IsoExternal.ScopeMode = GetScopeMode();

            _IsolateEvent.Raise();
        }

        private int GetScopeMode()
        {
            // 0 = Active View (Both), 1 = Host Only, 2 = Link Only (All Links)
            if (ScopeHost.IsChecked == true) return 1;
            if (ScopeLink.IsChecked == true) return 2;
            return 0; // Default to Active View
        }

        private bool ValidateInputs(out Category cat, out Parameter param,
            out string rule, out string value)
        {
            cat = CategoryCombo.SelectedItem as Category;
            param = ParameterCombo.SelectedItem as Parameter;
            rule = (RuleCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            value = ValueBox.Text?.Trim();

            if (cat == null) { MessageBox.Show(this, "Select a Category."); return false; }
            if (param == null) { MessageBox.Show(this, "Select a Parameter."); return false; }
            if (string.IsNullOrEmpty(rule)) { MessageBox.Show(this, "Select an Operator."); return false; }
            if (string.IsNullOrWhiteSpace(value)) { MessageBox.Show(this, "Enter a filter value."); return false; }

            return true;
        }

        private void OnClear(object sender, RoutedEventArgs e)
        {
            CategoryCombo.SelectedIndex = -1;
            ParameterCombo.ItemsSource = null;
            RuleCombo.SelectedIndex = -1;
            ValueBox.Clear();
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void PickColor_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.ColorDialog();

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _selectedColor = System.Windows.Media.Color.FromRgb(
                    dialog.Color.R,
                    dialog.Color.G,
                    dialog.Color.B);

                ColorPreview.Background = new SolidColorBrush(_selectedColor);

                HexText.Text = $"#{_selectedColor.R:X2}{_selectedColor.G:X2}{_selectedColor.B:X2}";
            }
        }

        private void AssignColor_Click(object sender, RoutedEventArgs e)
        {
            var cat = CategoryCombo.SelectedItem as Category;
            var param = ParameterCombo.SelectedItem as Parameter;
            var rule = (RuleCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            var value = ValueBox.Text;

            if (cat == null || param == null || string.IsNullOrEmpty(rule) || string.IsNullOrEmpty(value))
            {
                MessageBox.Show(this, "Complete all fields");
                return;
            }

            var revitColor = new Autodesk.Revit.DB.Color(
                _selectedColor.R,
                _selectedColor.G,
                _selectedColor.B);

            // 🔥 CRITICAL FIX → PASS STORAGE TYPE
            var ruleData = new SmartFilterColorHandler.FilterRuleData
            {
                ParameterId = param.Id,
                ParameterName = param.Definition.Name,
                Operator = rule,
                Value = value,
                StorageType = param.StorageType // 🔥 MUST HAVE
            };

            _smartHandler.CategoryId = cat.Id;
            _smartHandler.UseAndLogic = true;

            _smartHandler.Rules = new List<FilterRuleData> { ruleData };

            _smartHandler.RevitColor = revitColor;

            _smartHandler.ScopeMode = GetScopeMode();

            _smartEvent.Raise();
        }

        private void ClearOverride_Click(object sender, RoutedEventArgs e)
        {
            var cat = CategoryCombo.SelectedItem as Category;
            if (cat == null)
            {
                MessageBox.Show(this, "Select a Category first.");
                return;
            }

            _clearHandler.CategoryId = cat.Id;
            _clearHandler.ScopeMode = GetScopeMode();
            _clearEvent.Raise();

            ColorPreview.Background = Brushes.White;
        }
    }
}