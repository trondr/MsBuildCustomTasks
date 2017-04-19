using Microsoft.Win32;

namespace MSBuildCustomTasks.Common
{
    public class RegistryOperation
    {
        public static object GetValue(RegistryKey rootKey, string appKeyPath, string valueName)
        {
            using (var key = rootKey.OpenSubKey(appKeyPath, false))
            {
                return key?.GetValue(valueName);
            }
        }
    }
}