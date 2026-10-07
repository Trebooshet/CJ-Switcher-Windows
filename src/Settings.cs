using System;
using Microsoft.Win32;

namespace CJSwitcher
{
    /// <summary>Настройки в реестре (HKCU) и автозапуск.</summary>
    internal static class Settings
    {
        private const string SettingsKey = @"Software\CJ-Switcher";
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValue = "CJ-Switcher";

        public static bool AutoCorrect
        {
            get
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SettingsKey))
                    {
                        object value = key == null ? null : key.GetValue("AutoCorrect");
                        return value == null || Convert.ToInt32(value) != 0;
                    }
                }
                catch
                {
                    return true;
                }
            }
            set
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsKey))
                    {
                        key.SetValue("AutoCorrect", value ? 1 : 0, RegistryValueKind.DWord);
                    }
                }
                catch (Exception ex)
                {
                    Log.Write("Настройки: " + ex.Message);
                }
            }
        }

        public static bool AutoStart
        {
            get
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                    {
                        return key != null && key.GetValue(RunValue) != null;
                    }
                }
                catch
                {
                    return false;
                }
            }
            set
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                    {
                        if (value)
                        {
                            key.SetValue(RunValue, "\"" + System.Windows.Forms.Application.ExecutablePath + "\"");
                        }
                        else
                        {
                            key.DeleteValue(RunValue, false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Write("Автозапуск: " + ex.Message);
                }
            }
        }
    }
}
