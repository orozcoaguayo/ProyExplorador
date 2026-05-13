using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ProyExplorador.Helpers
{
    /// <summary>
    /// Aplica efectos visuales nativos DWM (modo oscuro, esquinas redondeadas).
    /// </summary>
    public static class AcrylicHelper
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr,
            ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        public static void EnableDarkMode(Window window)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).EnsureHandle();
                int value = 1;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
            }
            catch { }
        }

        public static void SetRoundedCorners(Window window)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).EnsureHandle();
                int value = DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref value, sizeof(int));
            }
            catch { }
        }
    }
}
