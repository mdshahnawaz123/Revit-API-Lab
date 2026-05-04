using System.Windows;
using System.Diagnostics;
using Autodesk.Revit.UI;
using System;
using RevitUI.UI.AccessRequest;
using DataLab.LicFolder;
using System.Windows.Media;

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

            UpdateLicenseDisplay();
        }

        private void UpdateLicenseDisplay()
        {
            var token = TokenService.LoadToken();
            if (token != null)
            {
                var remaining = (token.ExpiresUtc - DateTime.UtcNow).TotalDays;
                if (remaining > 0)
                {
                    TxtLicenseStatus.Text = $"{token.Plan.ToUpper()} LICENSE ACTIVE";
                    TxtDaysRemaining.Text = $"{(int)remaining} Days Remaining";
                    TxtDaysRemaining.Visibility = Visibility.Visible;
                    
                    // Style as Active (Green)
                    LicenseBorder.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DCFCE7"));
                    TxtLicenseStatus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#16A34A"));
                    StatusDot.Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#16A34A"));
                }
                else
                {
                    TxtLicenseStatus.Text = "LICENSE EXPIRED";
                    TxtDaysRemaining.Text = "0 Days Remaining";
                    TxtDaysRemaining.Visibility = Visibility.Visible;

                    // Style as Expired (Red)
                    LicenseBorder.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FEE2E2"));
                    TxtLicenseStatus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DC2626"));
                    StatusDot.Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DC2626"));
                }
            }
            else
            {
                TxtLicenseStatus.Text = "NO ACTIVE LICENSE";
                TxtDaysRemaining.Visibility = Visibility.Collapsed;

                // Style as Inactive (Gray)
                LicenseBorder.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F3F4F6"));
                TxtLicenseStatus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6B7280"));
                StatusDot.Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6B7280"));
            }
        }

        private void RequestAccess_Click(object sender, RoutedEventArgs e)
        {
            AccessRequestWindow requestWindow = new AccessRequestWindow();
            requestWindow.Owner = this;
            requestWindow.ShowDialog();
        }

        private void Support_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://stately-banoffee-4d959a.netlify.app/");
        }

        private void LinkedIn_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://www.linkedin.com/in/mohd-shahnawaz-5bb61798/");
        }

        private void Email_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            OpenUrl(e.Uri.AbsoluteUri);
            e.Handled = true;
        }

        private void CopyEmail_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText("bimdigitaldesign@gmail.com");
            TaskDialog.Show("Info", "Email copied to clipboard.");
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
