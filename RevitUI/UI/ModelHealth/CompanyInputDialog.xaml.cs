using System.Windows;

namespace RevitUI.UI.ModelHealth
{
    public partial class CompanyInputDialog : Window
    {
        public string CompanyName { get; private set; } = "BIM Digital Design";

        public CompanyInputDialog()
        {
            InitializeComponent();
            var settings = DataLab.SettingsManager.Load();
            CompanyName = settings.CompanyName;
            CompanyNameInput.Text = CompanyName;
            CompanyNameInput.Focus();
            CompanyNameInput.SelectAll();
        }

        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(CompanyNameInput.Text))
            {
                CompanyName = CompanyNameInput.Text;
                
                // Save to settings
                var settings = DataLab.SettingsManager.Load();
                settings.CompanyName = CompanyName;
                DataLab.SettingsManager.Save(settings);
            }
            DialogResult = true;
            Close();
        }
    }
}
