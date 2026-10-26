using System;
using System.IO;

namespace DrillIntel.Data.Objects.Logger
{
    /// <summary>
    /// File- and console-backed logger replacing legacy eVuMaxPro DynamicLogger.
    /// Safely writes process logs to the specified file path and debug listeners.
    /// If no file path is specified, defaults to ProgramData/DrillIntel/logs/{name}/{name}_{date}.txt.
    /// </summary>
    public class DynamicLogger : ILog
    {
        private readonly string _name;
        private readonly string? _filePath;
        private readonly object _lock = new();

        public string? FilePath => _filePath;
        public string Name => _name;

        public DynamicLogger(string name, string? filePath = null)
        {
            _name = string.IsNullOrWhiteSpace(name) ? "ProcessLog" : name;
            _filePath = !string.IsNullOrWhiteSpace(filePath)
                ? filePath
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "DrillIntel",
                    "logs",
                    _name,
                    $"{_name}_{DateTime.Now:yyyy_MM_dd}.txt");

            try
            {
                if (!string.IsNullOrWhiteSpace(_filePath))
                {
                    string? dir = Path.GetDirectoryName(_filePath);
                    if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                }
            }
            catch { }
        }

        // --- [CONVERTED FOR DRILLINTEL: Factory methods supporting legacy spelling 'GetDyamicLogger' and standard 'GetDynamicLogger'] ---
        public static ILog GetDyamicLogger(string name, string filePath) => new DynamicLogger(name, filePath);
        public static ILog GetDynamicLogger(string name, string filePath) => new DynamicLogger(name, filePath);

        public void Debug(string message) => Write("DEBUG", message);
        public void Info(string message) => Write("INFO", message);
        public void Warn(string message) => Write("WARN", message);
        public void Error(string message) => Write("ERROR", message);
        public void Error(string message, Exception? ex) =>
            Write("ERROR", ex != null ? $"{message}: {ex.Message}{Environment.NewLine}{ex.StackTrace}" : message);

        private void Write(string level, string message)
        {
            string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] [{_name}] {message}";
            System.Diagnostics.Debug.WriteLine(logLine);

            if (!string.IsNullOrWhiteSpace(_filePath))
            {
                try
                {
                    lock (_lock)
                    {
                        string? dir = Path.GetDirectoryName(_filePath);
                        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }
                        File.AppendAllText(_filePath, logLine + Environment.NewLine);
                    }
                }
                catch { }
            }
        }
    }
}

