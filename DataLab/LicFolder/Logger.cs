using System;
using System.IO;

namespace DataLab.LicFolder
{
    public static class Logger
    {
        private static readonly string LogFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "BDD_Tools", 
            "Logs"
        );

        private static readonly string LogFile = Path.Combine(LogFolder, "diagnostic_log.txt");

        public static void Log(string message, string category = "INFO")
        {
            try
            {
                if (!Directory.Exists(LogFolder))
                    Directory.CreateDirectory(LogFolder);

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string logLine = $"[{timestamp}] [{category}] {message}";

                // Keep file size reasonable (max 1MB)
                if (File.Exists(LogFile) && new FileInfo(LogFile).Length > 1024 * 1024)
                {
                    File.Move(LogFile, LogFile.Replace(".txt", $"_{DateTime.Now:yyyyMMddHHmmss}.txt"));
                }

                File.AppendAllText(LogFile, logLine + Environment.NewLine);
            }
            catch
            {
                // Silently fail if logging fails to avoid crashing the main app
            }
        }

        public static void LogError(Exception ex, string context)
        {
            Log($"{context}: {ex.Message}\nStackTrace: {ex.StackTrace}", "ERROR");
        }
    }
}
