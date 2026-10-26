using System;

namespace DrillIntel.Data.Objects.Logger
{
    /// <summary>
    /// Logging abstraction providing Debug, Info, Warn, and Error logging
    /// compatible with DrillIntel and legacy eVuMaxPro callers.
    /// </summary>
    public interface ILog
    {
        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message);
        void Error(string message, Exception? ex);
    }
}

