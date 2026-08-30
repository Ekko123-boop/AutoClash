using System;

namespace AutomatedClashRunner.Services.Interfaces
{
    public interface ILoggerService
    {
        void Log(string message);
        void LogWarning(string message);
        void LogError(string message, Exception ex = null);
    }
}
