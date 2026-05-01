using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using RevitUI.UI;

namespace RevitUI.UI.Worksets
{
    public partial class WorksetDashboard : Window, INotifyPropertyChanged
    {
        private readonly ExternalEvent _externalEvent;
        private readonly WorksetHandler _handler;

        public ObservableCollection<string> Categories { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> ElementTypes { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> Worksets { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<ElementData> Elements { get; set; } = new ObservableCollection<ElementData>();

        private bool _assignToAll;
        public bool AssignToAll
        {
            get => _assignToAll;
            set { _assignToAll = value; OnPropertyChanged(); }
        }

        private string _selectedWorkset;
        public string SelectedWorkset
        {
            get => _selectedWorkset;
            set { _selectedWorkset = value; OnPropertyChanged(); }
        }

        public WorksetDashboard(ExternalEvent externalEvent, WorksetHandler handler)
        {
            InitializeComponent();
            this.HideIcon();
            this.DataContext = this;
            _externalEvent = externalEvent;
            _handler = handler;

            DgElements.ItemsSource = Elements;
            
            // Trigger initial data fetch
            _handler.RequestType = WorksetHandler.RequestTypeEnum.FetchInitialData;
            _externalEvent.Raise();
        }

        public void UpdateInitialData(List<string> categories, List<string> worksets)
        {
            Categories.Clear();
            foreach (var c in categories) Categories.Add(c);
            
            Worksets.Clear();
            foreach (var w in worksets) Worksets.Add(w);
        }

        public void UpdateElementTypes(List<string> types)
        {
            ElementTypes.Clear();
            foreach (var t in types) ElementTypes.Add(t);
        }

        public void UpdateElements(List<ElementData> elements)
        {
            Elements.Clear();
            foreach (var e in elements) Elements.Add(e);
        }

        private void CbCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CbCategory.SelectedItem is string category)
            {
                _handler.SelectedCategory = category;
                _handler.RequestType = WorksetHandler.RequestTypeEnum.FetchTypes;
                _externalEvent.Raise();
            }
        }

        private void CbElementType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CbElementType.SelectedItem is string type)
            {
                _handler.SelectedType = type;
                _handler.RequestType = WorksetHandler.RequestTypeEnum.FetchElements;
                _externalEvent.Raise();
            }
        }

        private void BtnAssign_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedWorkset))
            {
                TaskDialog.Show("Error", "Please select a workset.");
                return;
            }

            _handler.TargetWorkset = SelectedWorkset;
            _handler.AssignToAll = AssignToAll;
            _handler.RequestType = WorksetHandler.RequestTypeEnum.AssignWorkset;
            _externalEvent.Raise();
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (Elements.Count == 0)
            {
                TaskDialog.Show("Info", "No elements to export.");
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = "WorksetExport.csv"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter(saveFileDialog.FileName))
                    {
                        sw.WriteLine("Element ID,Type,Workset");
                        foreach (var item in Elements)
                        {
                            sw.WriteLine($"{item.Id},{item.TypeName},{item.WorksetName}");
                        }
                    }
                    TaskDialog.Show("Success", "Export completed successfully.");
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Error", "Failed to export: " + ex.Message);
                }
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                this.DragMove();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            RevitUI.Command.WorksetCommand.Instance = null;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class ElementData
    {
        public string Id { get; set; }
        public string TypeName { get; set; }
        public string WorksetName { get; set; }
    }
}

