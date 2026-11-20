using System;

namespace Challenge.Logging
{
    public static class LoggerExtensions
    {
        public static void Debug(this ILogger logger, string message) => logger.Log(LogLevel.Debug, message);

        public static void Info(this ILogger logger, string message) => logger.Log(LogLevel.Info, message);

        public static void Warning(this ILogger logger, string message, Exception exception = null) => logger.Log(LogLevel.Warning, message, exception);

        public static void Error(this ILogger logger, string message, Exception exception = null) => logger.Log(LogLevel.Error, message, exception);

        /// <summary>A logger that prefixes every message with the name of the class writing it, e.g. "[MainViewModel]".</summary>
        public static ILogger ForCategory(this ILogger logger, string category) => new CategoryLogger(logger, category);
    }
}
