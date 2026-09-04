using Microsoft.Win32;
using System;
#nullable enable

namespace MCDSaveEdit
{
    public class RegistryTools
    {
        // Save a value.
        public static void SaveSetting(string app_name, string name, object value)
        {
            try
            {
                using RegistryKey? reg_key = Registry.CurrentUser.OpenSubKey("Software", true);
                using RegistryKey? sub_key = reg_key?.CreateSubKey(app_name);
                sub_key?.SetValue(name, value);
            }
            catch (Exception e) when (isUnavailableRegistryException(e))
            {
                // Preferences are optional; restricted environments can run without them.
            }
        }

        // Get a value.
        public static T GetSetting<T>(string app_name, string name, T default_value)
        {
            try
            {
                using RegistryKey? reg_key = Registry.CurrentUser.OpenSubKey("Software", false);
                using RegistryKey? sub_key = reg_key?.OpenSubKey(app_name, false);
                object? value = sub_key?.GetValue(name, default_value);
                return value is T typedValue ? typedValue : default_value;
            }
            catch (Exception e) when (isUnavailableRegistryException(e))
            {
                return default_value;
            }
        }

        // Delete a value.
        public static void DeleteSetting(string app_name, string name)
        {
            try
            {
                using RegistryKey? reg_key = Registry.CurrentUser.OpenSubKey("Software", true);
                using RegistryKey? sub_key = reg_key?.OpenSubKey(app_name, true);
                sub_key?.DeleteValue(name, false);
            }
            catch (Exception e) when (isUnavailableRegistryException(e))
            {
                // Preferences are optional; restricted environments can run without them.
            }
        }

        private static bool isUnavailableRegistryException(Exception exception)
        {
            return exception is UnauthorizedAccessException
                || exception is System.Security.SecurityException
                || exception is System.IO.IOException;
        }
    }
}
