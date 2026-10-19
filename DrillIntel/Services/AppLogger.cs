using System;
using System.Diagnostics;

namespace DrillIntel.Services;

/// <summary>
/// Contract for application-wide logging.
/// </summary>
public interface IAppLogger
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message, Exception? ex = null);
}

/// <summary>
/// Default implementation of IAppLogger routing to System.Diagnostics.Trace and Debug.
/// </summary>
public class AppLogger : IAppLogger
{
    private readonly string _category;

    public AppLogger(string category = "DrillIntel")
    {
        _category = category;
    }

    public void LogInfo(string message)
    {
        string formatted = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [INFO] [{_category}] {message}";
        Trace.TraceInformation(formatted);
        Debug.WriteLine(formatted);
    }

    public void LogWarning(string message)
    {
        string formatted = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [WARN] [{_category}] {message}";
        Trace.TraceWarning(formatted);
        Debug.WriteLine(formatted);
    }

    public void LogError(string message, Exception? ex = null)
    {
        string formatted = ex != null
            ? $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERROR] [{_category}] {message}\nException: {ex}"
            : $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERROR] [{_category}] {message}";
        Trace.TraceError(formatted);
        Debug.WriteLine(formatted);
    }
}
