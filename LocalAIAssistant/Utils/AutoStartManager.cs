using Microsoft.Win32;

namespace LocalAIAssistant.Utils
{
    public static class AutoStartManager
    {
        private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "LocalAIAssistant";

        public static void EnableAutoStart()
        {
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath)) return;

            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            key?.SetValue(AppName, $"\"{exePath}\" --minimized");
        }

        public static void DisableAutoStart()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            key?.DeleteValue(AppName, false);
        }

        public static bool IsAutoStartEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            return key?.GetValue(AppName) != null;
        }
    }
}
