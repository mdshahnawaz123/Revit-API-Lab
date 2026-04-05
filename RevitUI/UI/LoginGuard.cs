using DataLab.LicFolder;
using System.Threading.Tasks;
using System.Windows;

namespace RevitUI.UI
{
    public static class LoginGuard
    {
        private static bool _isAuthenticated = false;

        /// <summary>
        /// Central check to ensure the user is authorized.
        /// It will attempt auto-login first, then show the LoginWindow if needed.
        /// </summary>
        public static bool IsAuthorized()
        {
            // 1. Session check — if already logged in this Revit session, skip everything
            if (_isAuthenticated) return true;

            // 2. Auto-login check — attempts to load from encrypted local token
            //    Using Task.Run since Revit commands are synchronous and TryAutoLoginAsync is async
            var autoLoginResult = Task.Run(async () => await LicenseManager.TryAutoLoginAsync()).Result;

            if (autoLoginResult.Success)
            {
                _isAuthenticated = true;
                return true;
            }

            // 3. Manual login — show modal LoginWindow if auto-login fails
            var loginWindow = new LoginWindow();
            // Use HideIcon to keep it clean (consistent with other windows)
            loginWindow.HideIcon();

            bool? result = loginWindow.ShowDialog();
            if (result == true)
            {
                _isAuthenticated = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resets the authentication state (useful for forcing relocation/logout tests).
        /// </summary>
        public static void Reset() => _isAuthenticated = false;
    }
}
