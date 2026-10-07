\xEF\xBB\xBFusing System;
using System.IO;

namespace CJSwitcher
{
    /// <summary>Небольшой журнал ошибок: %APPDATA%\CJ-Switcher\log.txt</summary>
    internal static class Log
    {
        private static readonly string Dir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CJ-Switcher");

        public static string FilePath
        {
            get { return Path.Combine(Dir, "log.txt"); }
        }

        public static void Write(string message)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > 200000) File.Delete(FilePath);
                File.AppendAllText(FilePath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
