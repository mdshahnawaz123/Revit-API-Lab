using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace RevitUI.UI
{
    public static class WindowExtensions
    {
        private static readonly Dictionary<Type, Window> _instances = new Dictionary<Type, Window>();

        /// <summary>
        /// Shows a Window as a singleton. Optionally removes the Revit/WPF icon from its title bar.
        /// </summary>
        /// <typeparam name="T">The type of the Window</typeparam>
        /// <param name="createAction">A factory function that creates the window if it doesn't exist.</param>
        /// <param name="hideIcon">If true, removes the default icon. If false, keeps it.</param>
        public static void ShowSingleton<T>(Func<T> createAction, bool hideIcon = true) where T : Window
        {
            Type type = typeof(T);

            if (_instances.TryGetValue(type, out Window window) && window != null)
            {
                if (!window.IsVisible)
                    window.Show();

                if (window.WindowState == WindowState.Minimized)
                    window.WindowState = WindowState.Normal;

                window.Activate();
                window.Focus();
                return;
            }

            // Create new instance
            var newWindow = createAction();
            _instances[type] = newWindow;

            // Remove from tracking dictionary when closed
            newWindow.Closed += (s, e) => _instances.Remove(type);

            // Hide the icon automatically if requested
            if (hideIcon)
            {
                newWindow.HideIcon();
            }

            newWindow.Show();
        }
    }
}
