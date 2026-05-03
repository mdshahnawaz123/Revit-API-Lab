using System.Windows;
using System.Diagnostics;
using Autodesk.Revit.UI;
using System;

namespace RevitUI.UI.About
{
    public partial class AboutDashboard : Window
    {
        public AboutDashboard(UIApplication uiApp)
        {
            InitializeComponent();
            
            // Version Info
            var assemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            VersionText.Text = "Version " + assemblyVersion;

            // System Diagnostic Info
            TxtSystemInfo.Text = $"{uiApp.Application.VersionName} | Assembly: {assemblyVersion} | {Environment.OSVersion.VersionString}";
        }

        private void Support_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://stately-banoffee-4d959a.netlify.app/");
        }

        private void LinkedIn_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://www.linkedin.com/in/mohd-shahnawaz-5bb61798/");
        }

        private void OpenUrl(string url)
        {
            try {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            } catch { }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
