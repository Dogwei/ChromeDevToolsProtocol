using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace ChromeDevToolsProtocol
{
    /// <summary>
    /// This class searches for the Chrome or Chromium executables cross-platform.
    /// </summary>
    public static class ChromeFinder
    {
        static readonly (string KeyName, string ValueName)[] WindowsRegistryItems =
        {
            (@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Google Chrome", "InstallLocation"),
            (@"HKEY_CURRENT_USER\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Google Chrome", "InstallLocation"),
            (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe", "Path"),
            (@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe", "Path"),
        };

        #region GetApplicationDirectories
        private static void GetApplicationDirectories(ICollection<string> directories)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // c:\Program Files\Google\Chrome\Application\
                const string subDirectory = "Google\\Chrome\\Application";
                directories.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), subDirectory));
                directories.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), subDirectory));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                directories.Add("/usr/local/sbin");
                directories.Add("/usr/local/bin");
                directories.Add("/usr/sbin");
                directories.Add("/usr/bin");
                directories.Add("/sbin");
                directories.Add("/bin");
                directories.Add("/opt/google/chrome");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                throw new Exception("Finding Chrome on MacOS is currently not supported, please contact the programmer.");
        }
        #endregion

        #region GetAppPath
        private static string GetAppPath()
        {
            var appPath = AppDomain.CurrentDomain.BaseDirectory!;

            // ReSharper disable once PossibleNullReferenceException
            if (appPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                return appPath;
            return appPath + Path.DirectorySeparatorChar;
        }
        #endregion

        #region Find
        /// <summary>
        /// Tries to find Chrome
        /// </summary>
        /// <returns></returns>
        public static string? Find()
        {
            // For Windows we first check the registry. This is the safest
            // method and also considers non-default installation locations.
            // Note that Chrome x64 currently (February 2019) also gets installed
            // in Program Files (x86) and uses the same registry key!
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                foreach (var (keyName, valueName) in WindowsRegistryItems)
                {
                    var value = Registry.GetValue(keyName, valueName, string.Empty)?.ToString();

                    if (!string.IsNullOrEmpty(value))
                    {
                        if (File.Exists(value))
                        {
                            return value;
                        }

                        var path = Path.Combine(value, "chrome.exe");

                        if (File.Exists(path))
                        {
                            return path;
                        }
                    }
                }
            }

            // Collect the usual executable names
            var exeNames = new List<string>();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                exeNames.Add("chrome.exe");
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                exeNames.Add("google-chrome");
                exeNames.Add("chrome");
                exeNames.Add("chromium");
                exeNames.Add("chromium-browser");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                exeNames.Add("Google Chrome.app/Contents/MacOS/Google Chrome");
                exeNames.Add("Chromium.app/Contents/MacOS/Chromium");
                exeNames.Add("/Applications/Google Chrome.app/Contents/MacOS/Google Chrome");
            }

            // Check the directory of this assembly/application
            var currentPath = GetAppPath();

            foreach (var exeName in exeNames)
            {
                var path = Path.Combine(currentPath, exeName);
                if (File.Exists(path))
                    return path;
            }

            // Search common software installation directories
            // for the various exe names.

            var directories = new List<string>();

            GetApplicationDirectories(directories);

            foreach (var exeName in exeNames)
            {
                foreach (var directory in directories)
                {
                    var path = Path.Combine(directory, exeName);
                    if (File.Exists(path))
                        return path;
                }
            }

            return null;
        }
        #endregion
    }
}