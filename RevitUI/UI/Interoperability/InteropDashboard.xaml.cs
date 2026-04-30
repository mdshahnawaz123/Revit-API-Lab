using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using Autodesk.Revit.UI;

namespace RevitUI.UI.Interoperability
{
    public partial class InteropDashboard : Window
    {
        private readonly ExternalEvent _externalEvent;
        private readonly InteropHandler _handler;
        public ObservableCollection<CategoryStat> Stats { get; set; } = new ObservableCollection<CategoryStat>();

        public InteropDashboard(ExternalEvent externalEvent, InteropHandler handler)
        {
            InitializeComponent();
            this.HideIcon();
            _externalEvent = externalEvent;
            _handler = handler;
            
            ItemsCategory.ItemsSource = Stats;
            
            // Initial scan
            _externalEvent.Raise();
        }

        public void UpdateStats(List<CategoryStat> newStats)
        {
            Stats.Clear();
            foreach (var s in newStats) Stats.Add(s);
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
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
            RevitUI.Command.InteropCommand.Instance = null;
        }
    }

    public class CategoryStat
    {
        public string CategoryName { get; set; }
        public int ElementCount { get; set; }
        public double CoveragePercent { get; set; }
        public string CoverageText => $"{CoveragePercent}% Complete";
    }
}
