using System;
using System.Windows;
using Autodesk.Revit.UI;

namespace RevitUI.UI.Purge
{
    public partial class PurgeDashboard : Window
    {
        private readonly ExternalEvent _externalEvent;
        private readonly PurgeHandler _handler;

        public PurgeDashboard(ExternalEvent externalEvent, PurgeHandler handler)
        {
            InitializeComponent();
            this.HideIcon();
            _externalEvent = externalEvent;
            _handler = handler;
        }

        private void BtnPurge_Click(object sender, RoutedEventArgs e)
        {
            _handler.PurgeTemplates = ChkTemplates.IsChecked == true;
            _handler.PurgeFilters = ChkFilters.IsChecked == true;
            _handler.PurgeStyles = ChkStyles.IsChecked == true;
            _handler.PurgeRooms = ChkRooms.IsChecked == true;
            _handler.PurgeEmptyViews = ChkViews.IsChecked == true;

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
            RevitUI.Command.PurgeCommand.Instance = null;
        }
    }
}
