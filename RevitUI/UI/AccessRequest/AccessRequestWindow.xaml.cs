using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using DataLab.LicFolder;
using System.Threading.Tasks;
using System;

namespace RevitUI.UI.AccessRequest
{
    public partial class AccessRequestWindow : Window
    {
        public AccessRequestWindow()
        {
            InitializeComponent();
        }

        private void ComboDuration_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboDuration.SelectedItem is ComboBoxItem selectedItem)
            {
                if (selectedItem.Content.ToString() == "Lifetime")
                {
                    MessageBox.Show("Please write an email to bimdigitaldesign@gmail.com for more details regarding lifetime access.", 
                                    "Lifetime Access Request", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Information);
                }
            }
        }

        private async void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            string username = TxtUsername.Text;
            string email = TxtEmail.Text;
            string password = TxtPassword.Password;
            string duration = (ComboDuration.SelectedItem as ComboBoxItem)?.Content.ToString();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Please fill all the required fields.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ValidatePassword(password))
            {
                MessageBox.Show("Password is too weak!\n\nIt must contain:\n- At least 8 characters\n- At least 1 uppercase letter\n- At least 1 lowercase letter\n- At least 1 number", "Weak Password", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnSubmit.IsEnabled = false;
            BtnSubmit.Content = "Submitting...";

            try
            {
                // 1. Prepare User Record
                var newUser = new UserRecord
                {
                    Username = username,
                    EmailId = email,
                    Password = password,
                    Active = true,
                    Plan = duration,
                    Expires = CalculateExpiry(duration),
                    Machines = new List<string> { MachineHelper.GetMachineId() }
                };

                // 2. Attempt Auto-fill to GitHub (Internal/Silent)
                var (success, errorMessage) = await GithubService.AddUserToGithubAsync(newUser);

                if (success)
                {
                    MessageBoxResult result = MessageBox.Show(
                        $"Request submitted successfully!\n\nIMPORTANT: Please save your login details so you don't forget them.\n\nWould you like to save these details as a text file on your Desktop?", 
                        "Save Login Details", MessageBoxButton.YesNo, MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        try
                        {
                            string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "B_Lab_Login_Details.txt");
                            System.IO.File.WriteAllText(path, $"B-Lab Suite Login Details\n=======================\nUsername: {username}\nEmail: {email}\nPassword: {password}\nPlan: {duration}\nRegistered: {DateTime.Now}");
                            MessageBox.Show($"Details saved to:\n{path}", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Could not save to Desktop: {ex.Message}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }

                    this.Close();
                }
                else
                {
                    MessageBox.Show($"There was an error submitting your request.\n\nDetails: {errorMessage}\n\nPlease try again later or contact support directly.", 
                                    "Submission Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An unexpected error occurred in the UI: {ex.Message}", 
                                "Submission Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Ensure the button is re-enabled if we didn't close the window
                BtnSubmit.IsEnabled = true;
                BtnSubmit.Content = "Submit Request";
            }
        }

        private DateTime CalculateExpiry(string duration)
        {
            if (duration == "7 Days") return DateTime.Now.AddDays(7);
            if (duration == "30 Days") return DateTime.Now.AddDays(30);
            if (duration == "Lifetime") return DateTime.Now.AddYears(100);
            return DateTime.Now.AddDays(30);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
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

        private bool _isPasswordSyncing = false;

        private void ChkShowPassword_Click(object sender, RoutedEventArgs e)
        {
            if (ChkShowPassword.IsChecked == true)
            {
                TxtPasswordVisible.Text = TxtPassword.Password;
                TxtPassword.Visibility = Visibility.Collapsed;
                TxtPasswordVisible.Visibility = Visibility.Visible;
            }
            else
            {
                TxtPassword.Password = TxtPasswordVisible.Text;
                TxtPasswordVisible.Visibility = Visibility.Collapsed;
                TxtPassword.Visibility = Visibility.Visible;
            }
        }

        private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!_isPasswordSyncing)
            {
                _isPasswordSyncing = true;
                TxtPasswordVisible.Text = TxtPassword.Password;
                _isPasswordSyncing = false;
            }
        }

        private void TxtPasswordVisible_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isPasswordSyncing)
            {
                _isPasswordSyncing = true;
                TxtPassword.Password = TxtPasswordVisible.Text;
                _isPasswordSyncing = false;
            }
        }
    }
}
