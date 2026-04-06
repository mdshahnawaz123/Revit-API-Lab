using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitUI.ExternalCommand.ParameterFilter;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace RevitUI.UI
{
    public partial class ParamFilter : Window
    {
        // ✅ Single static instance
        private static ParamFilter? _instance;

        private readonly Document _doc;
        private readonly UIDocument _uidoc;

        private readonly ExternalEvent _applyFilterEvent;
        private readonly ParamExternal _paramExternal;

        private readonly ExternalEvent _IsolateEvent;
        private readonly IsolateExternal _IsoExternal;

        private List<Parameter> _currentParameters = new();

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

            _IsolateEvent.Raise();
        }

        private bool ValidateInputs(out Category cat, out Parameter param,
            out string rule, out string value)
        {
            cat = CategoryCombo.SelectedItem as Category;
            param = ParameterCombo.SelectedItem as Parameter;
            rule = (RuleCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            value = ValueBox.Text?.Trim();

            if (cat == null) { MessageBox.Show("Select a Category."); return false; }
            if (param == null) { MessageBox.Show("Select a Parameter."); return false; }
            if (string.IsNullOrEmpty(rule)) { MessageBox.Show("Select an Operator."); return false; }
            if (string.IsNullOrWhiteSpace(value)) { MessageBox.Show("Enter a filter value."); return false; }

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
    }
}