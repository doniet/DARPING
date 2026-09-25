using System;
using System.Reflection;
using Microsoft.Win32;

namespace DarPing
{
    public static class StartupManager
    {
        private const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string ValueName = "DarPing";

        public static void Apply(bool enabled)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (key == null) return;
                    if (enabled)
                    {
                        var executable = Assembly.GetEntryAssembly().Location;
                        key.SetValue(ValueName, "\"" + executable + "\"");
                    }
                    else key.DeleteValue(ValueName, false);
                }
            }
            catch { }
        }
    }
}