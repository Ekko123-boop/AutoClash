using System;
using System.IO;
using AutomatedClashRunner.Services.Interfaces;

namespace AutomatedClashRunner.Services
{
    public class LoggerService : ILoggerService
    {
        private static readonly object _lock = new object();
        private static readonly string _logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CypherNavisTools", "Logs");

        public static LoggerService Instance { get; } = new LoggerService();

        public void Log(string message) => WriteEntry("INFO", message);
        public void LogWarning(string message) => WriteEntry("WARN", message);
        public void LogError(string message, Exception ex = null)
        {
            string fullMessage = ex != null ? $"{message} | Exception: {ex.Message}{Environment.NewLine}{ex.StackTrace}" : message;
            WriteEntry("ERROR", fullMessage);
        }

        public static void LogStatic(string message) => Instance.Log(message);
        public static void LogWarningStatic(string message) => Instance.LogWarning(message);
        public static void LogErrorStatic(string message, Exception ex = null) => Instance.LogError(message, ex);

        private void WriteEntry(string level, string message)
        {
            try
            {
                lock (_lock)
                {
                    if (!Directory.Exists(_logDir))
                    {
                        Directory.CreateDirectory(_logDir);
                    }

                    string logPath = Path.Combine(_logDir, $"session_{DateTime.Now:yyyy-MM-dd}.log");
                    
                    // Rotate if file exceeds 10MB
                    if (File.Exists(logPath) && new FileInfo(logPath).Length > 10 * 1024 * 1024)
                    {
                        string backupPath = Path.Combine(_logDir, $"session_{DateTime.Now:yyyy-MM-dd_HHmmss}.log");
                        File.Move(logPath, backupPath);
                    }

                    string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";
                    File.AppendAllText(logPath, entry);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"AutomatedClashRunner Logger failure: {ex.Message}");
            }
        }
    }
}
