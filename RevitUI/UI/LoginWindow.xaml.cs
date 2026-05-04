using DataLab.LicFolder;
using RevitUI.UI.AccessRequest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace RevitUI.UI
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            this.HideIcon();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void SignIn_Click(object sender, RoutedEventArgs e)
        {
            // Clear previous error
            TB_Error.Text = "";
            TB_Error.Foreground = new SolidColorBrush(Colors.Red);

            // Basic empty field check before hitting network
            if (string.IsNullOrWhiteSpace(TB_Username.Text))
            {
                TB_Error.Text = "Please enter your username.";
                TB_Username.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(PB_Password.Password))
            {
                TB_Error.Text = "Please enter your password.";
                PB_Password.Focus();
                return;
            }

            // Disable UI and show loading state
            SetLoadingState(true);

            string errorMessage = "";
            var ok = await LicenseManager.LoginAsync(
                TB_Username.Text.Trim(),
                PB_Password.Password,
                m =>
                {
                    errorMessage = m;
                });

            SetLoadingState(false);

            if (ok)
            {
                // Show trial days remaining as a friendly notice
                int days = LicenseManager.GetTrialDaysRemaining(TB_Username.Text.Trim());
                if (days >= 0 && days <= 30)
                {
                    // Show warning if 7 days or fewer remain
                    if (days <= 7)
                    {
                        TB_Error.Foreground = new SolidColorBrush(Colors.OrangeRed);
                        TB_Error.Text = days == 0
                            ? "Your trial expires today!"
                            : $"Warning: Only {days} trial day(s) remaining.";

                        // Give user a moment to read the warning before closing
                        await System.Threading.Tasks.Task.Delay(1800);
                    }
                }

                DialogResult = true;
                Close();
            }
            else
            {
                // Display the error message that was captured
                TB_Error.Text = errorMessage;
                TB_Error.Foreground = new SolidColorBrush(Colors.Red);
            }
        }

        private void RequestAccess_Click(object sender, RoutedEventArgs e)
        {
            AccessRequestWindow requestWindow = new AccessRequestWindow();
            requestWindow.Owner = this;
            requestWindow.ShowDialog();
        }

        private void ShowForgot_Click(object sender, RoutedEventArgs e)
        {
            LoginPanel.Visibility = Visibility.Collapsed;
            ForgotPanel.Visibility = Visibility.Visible;
            TB_ForgotError.Text = "";
            TB_ForgotUsername.Text = TB_Username.Text; // Pre-fill
        }

        private void BackToLogin_Click(object sender, RoutedEventArgs e)
        {
            ForgotPanel.Visibility = Visibility.Collapsed;
            LoginPanel.Visibility = Visibility.Visible;
        }

        private async void ResetPassword_Click(object sender, RoutedEventArgs e)
        {
            TB_ForgotError.Text = "";
            TB_ForgotError.Foreground = new SolidColorBrush(Colors.Red);

            string user = TB_ForgotUsername.Text.Trim();
            string email = TB_ForgotEmail.Text.Trim();
            string newPass = PB_ForgotNewPassword.Password;

            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(newPass))
            {
                TB_ForgotError.Text = "Please fill in all fields.";
                return;
            }

            if (!ValidatePassword(newPass))
            {
                TB_ForgotError.Text = "Password is too weak!\nMust be 8+ chars, 1 uppercase, 1 lowercase, 1 number.";
                return;
            }

            IsEnabled = false;
            TB_ForgotError.Foreground = new SolidColorBrush(Colors.Gray);
            TB_ForgotError.Text = "Resetting password, please wait...";

            var (success, msg) = await GithubService.ResetPasswordAsync(user, email, newPass);

            IsEnabled = true;

            if (success)
            {
                MessageBox.Show("Password reset successfully! You can now log in with your new password.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                TB_Username.Text = user;
                PB_Password.Password = "";
                BackToLogin_Click(null, null);
            }
            else
            {
                TB_ForgotError.Foreground = new SolidColorBrush(Colors.Red);
                TB_ForgotError.Text = msg;
            }
        }

        private void SetLoadingState(bool isLoading)
        {
            IsEnabled = !isLoading;

            if (isLoading)
            {
                TB_Error.Foreground = new SolidColorBrush(Colors.Gray);
                TB_Error.Text = "Signing in, please wait...";
                // If you have a loading spinner in XAML, show it here:
                // Spinner.Visibility = Visibility.Visible;
            }
            else
            {
                TB_Error.Text = "";
                // Spinner.Visibility = Visibility.Collapsed;
            }
        }

        private bool ValidatePassword(string pass)
        {
            if (string.IsNullOrEmpty(pass) || pass.Length < 8) return false;
            
            bool hasUpper = false;
            bool hasLower = false;
            bool hasDigit = false;

            foreach (char c in pass)
            {
                if (char.IsUpper(c)) hasUpper = true;
                if (char.IsLower(c)) hasLower = true;
                if (char.IsDigit(c)) hasDigit = true;
            }

            return hasUpper && hasLower && hasDigit;
        }

        private bool _isForgotSyncing = false;

        private void ChkForgotShowPassword_Click(object sender, RoutedEventArgs e)
        {
            if (ChkForgotShowPassword.IsChecked == true)
            {
                TB_ForgotNewPasswordVisible.Text = PB_ForgotNewPassword.Password;
                PB_ForgotNewPassword.Visibility = Visibility.Collapsed;
                TB_ForgotNewPasswordVisible.Visibility = Visibility.Visible;
            }
            else
            {
                PB_ForgotNewPassword.Password = TB_ForgotNewPasswordVisible.Text;
                TB_ForgotNewPasswordVisible.Visibility = Visibility.Collapsed;
                PB_ForgotNewPassword.Visibility = Visibility.Visible;
            }
        }

        private void PB_ForgotNewPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!_isForgotSyncing)
            {
                _isForgotSyncing = true;
                TB_ForgotNewPasswordVisible.Text = PB_ForgotNewPassword.Password;
                _isForgotSyncing = false;
            }
        }

        private void TB_ForgotNewPasswordVisible_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isForgotSyncing)
            {
                _isForgotSyncing = true;
                PB_ForgotNewPassword.Password = TB_ForgotNewPasswordVisible.Text;
                _isForgotSyncing = false;
            }
        }
        private bool _isPasswordSyncing = false;

        private void ChkShowPassword_Click(object sender, RoutedEventArgs e)
        {
            if (ChkShowPassword.IsChecked == true)
            {
                TB_PasswordVisible.Text = PB_Password.Password;
                PB_Password.Visibility = Visibility.Collapsed;
                TB_PasswordVisible.Visibility = Visibility.Visible;
            }
            else
            {
                PB_Password.Password = TB_PasswordVisible.Text;
                TB_PasswordVisible.Visibility = Visibility.Collapsed;
                PB_Password.Visibility = Visibility.Visible;
            }
        }

        private void PB_Password_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!_isPasswordSyncing)
            {
                _isPasswordSyncing = true;
                TB_PasswordVisible.Text = PB_Password.Password;
                _isPasswordSyncing = false;
            }
        }

        private void TB_PasswordVisible_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isPasswordSyncing)
            {
                _isPasswordSyncing = true;
                PB_Password.Password = TB_PasswordVisible.Text;
                _isPasswordSyncing = false;
            }
        }
    }
}
