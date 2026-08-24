using System;
using System.IO;

namespace AutomatedClashRunner.Services
{
    public static class LoggerService
    {
        public static void Log(string message)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutomatedClashRunner");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "session_logs.txt"), $"{DateTime.Now}: {message}\n");
            }
            catch { }
        }
    }
}
