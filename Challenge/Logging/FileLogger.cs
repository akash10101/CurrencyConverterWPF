using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Challenge.Logging
{
    /// <summary>
    /// Appends one line per message to a daily file (app-yyyyMMdd.log) in <see cref="Directory"/>, 
    /// </summary>
    public sealed class FileLogger : ILogger
    {
        private readonly object _gate = new object();
        private readonly LogLevel _minimumLevel;
        private readonly Func<DateTime> _now;

        public FileLogger(string directory, LogLevel minimumLevel = LogLevel.Info, Func<DateTime> now = null)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("A log directory is required.", nameof(directory));
            }

            Directory = directory;
            _minimumLevel = minimumLevel;
            _now = now ?? (() => DateTime.Now);
        }

        public static string DefaultDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CurrencyConverter", "Logs");

        public string Directory { get; }

        public string CurrentFilePath => Path.Combine(Directory, "app-" + _now().ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");

        public void Log(LogLevel level, string message, Exception exception = null)
        {
            if (level < _minimumLevel)
            {
                return;
            }

            var now = _now();
            var line = new StringBuilder()
                .Append(now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
                .Append(" [").Append(level.ToString().ToUpperInvariant().PadRight(7)).Append("] ")
                .Append("(T").Append(Thread.CurrentThread.ManagedThreadId).Append(") ")
                .Append(message);
            if (exception != null)
            {
                line.AppendLine().Append(exception);
            }

            var text = line.ToString();
            Debug.WriteLine(text);

            try
            {
                lock (_gate)
                {
                    System.IO.Directory.CreateDirectory(Directory);
                    File.AppendAllText(CurrentFilePath, text + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
