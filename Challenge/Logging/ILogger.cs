using System;

namespace Challenge.Logging
{
    /// <summary>Writes diagnostic messages. Implementations must be thread-safe and must never throw.</summary>
    public interface ILogger
    {
        void Log(LogLevel level, string message, Exception exception = null);
    }
}
