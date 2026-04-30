using System;
using System.Windows;

namespace RevitUI.UI
{
    public partial class AboutMe : Window
    {
        public AboutMe()
        {
            InitializeComponent();
            
            // Note: Update these values or keep placeholders
            // TxtPhone.Text = "+91 12345 67890";
            // TxtQual.Text = "B.Arch / Senior BIM Developer";
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Allow window dragging
        protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            this.DragMove();
        }
    }
}
