using System;
using System.Collections.Generic;
using System.Linq;
using Challenge.Logging;

namespace Challenge.Tests
{
    /// <summary>Keeps every message so tests can check what was logged.</summary>
    internal sealed class RecordingLogger : ILogger
    {
        private readonly List<string> _lines = new List<string>();

        public IReadOnlyList<string> Lines
        {
            get { lock (_lines) { return _lines.ToList(); } }
        }

        public void Log(LogLevel level, string message, Exception exception = null)
        {
            lock (_lines)
            {
                _lines.Add(level + " " + message + (exception == null ? string.Empty : " | " + exception.GetType().Name));
            }
        }

        public bool Contains(string fragment) => Lines.Any(line => line.Contains(fragment));
    }
}
