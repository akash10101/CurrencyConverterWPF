using System;

namespace Challenge.Logging
{
    /// <summary>Prefixes every message with a category, e.g. "[MainViewModel]". Created by <see cref="LoggerExtensions.ForCategory"/>.</summary>
    internal sealed class CategoryLogger : ILogger
    {
        private readonly ILogger _inner;
        private readonly string _prefix;

        public CategoryLogger(ILogger inner, string category)
        {
            _inner = inner ?? NullLogger.Instance;
            _prefix = "[" + category + "] ";
        }

        public void Log(LogLevel level, string message, Exception exception = null) => _inner.Log(level, _prefix + message, exception);
    }
}
