using System;

namespace Challenge.Logging
{
    /// <summary>Discards everything. The default wherever no logger is passed in (e.g. unit tests).</summary>
    public sealed class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new NullLogger();

        private NullLogger()
        {
        }

        public void Log(LogLevel level, string message, Exception exception = null)
        {
        }
    }
}
