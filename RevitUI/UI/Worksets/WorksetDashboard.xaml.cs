using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using Autodesk.Revit.UI;

namespace RevitUI.UI.Worksets
{
    public partial class WorksetDashboard : Window
    {
        private readonly ExternalEvent _externalEvent;
        private readonly WorksetHandler _handler;

        public ObservableCollection<WorksetMapping> Mappings { get; set; } = new ObservableCollection<WorksetMapping>();
        public List<string> WorksetList { get; set; } = new List<string>();

        public WorksetDashboard(ExternalEvent externalEvent, WorksetHandler handler)
        {
            InitializeComponent();
            this.HideIcon();
            _externalEvent = externalEvent;
            _handler = handler;

            DgMapping.ItemsSource = Mappings;
            
            // Initial load of project data
            _externalEvent.Raise();
        }

        public void LoadData(List<WorksetMapping> mappings, List<string> worksets)
        {
            WorksetList.Clear();
            foreach (var w in worksets) WorksetList.Add(w);
            
            Mappings.Clear();
            foreach (var m in mappings) Mappings.Add(m);
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            _handler.ApplyMapping = true;
            _externalEvent.Raise();
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
            RevitUI.Command.WorksetCommand.Instance = null;
        }
    }

    public class WorksetMapping
    {
        public string CategoryName { get; set; }
        public string TargetWorkset { get; set; }
    }
}
