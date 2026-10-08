using System;
using Microsoft.Win32;

namespace LastFolder.Services
{
    /// <summary>"Start with Windows" setting via HKCU\...\Run.</summary>
    internal static class AutoStart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "LastFolder";

        private static string Command => $"\"{Environment.ProcessPath}\" --silent";

        public static bool IsEnabled
        {
            get
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
                return key?.GetValue(ValueName) is string value &&
                       value.Equals(Command, StringComparison.OrdinalIgnoreCase);
            }
        }

        public static void Set(bool enabled)
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
            if (enabled) key.SetValue(ValueName, Command);
            else key.DeleteValue(ValueName, false);
        }
    }
}
