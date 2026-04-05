using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace RevitUI.UI
{
    public static class WindowUtils
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_DLGMODALFRAME = 0x0001;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_FRAMECHANGED = 0x0020;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int width, int height, uint flags);

        /// <summary>
        /// Hides the Revit icon from the window title bar by removing the DLGMODALFRAME style.
        /// This is the standard Revit plugin way to avoid the "R" icon.
        /// </summary>
        public static void HideIcon(this Window window)
        {
            // Get window handle
            IntPtr hwnd = new WindowInteropHelper(window).Handle;

            // Use SourceInitialized if the handle is not yet available
            if (hwnd == IntPtr.Zero)
            {
                window.SourceInitialized += (s, e) =>
                {
                    hwnd = new WindowInteropHelper(window).Handle;
                    ApplyHideIcon(hwnd);
                };
            }
            else
            {
                ApplyHideIcon(hwnd);
            }
        }

        private const int WM_SETICON = 0x0080;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, IntPtr lParam);

        private static void ApplyHideIcon(IntPtr hwnd)
        {
            // 1. Remove the icon from the title bar via window style
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_DLGMODALFRAME);

            // 2. Set both small and big icons to null
            SendMessage(hwnd, WM_SETICON, ICON_SMALL, IntPtr.Zero);
            SendMessage(hwnd, WM_SETICON, ICON_BIG, IntPtr.Zero);

            // 3. Update window frame
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
        }
    }
}
