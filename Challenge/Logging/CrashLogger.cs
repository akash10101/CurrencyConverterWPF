using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Challenge.Logging
{
    public sealed class CrashLogger
    {
        private readonly ILogger _logger;
        private readonly object _gate = new object();
        private Exception _lastLogged;

        public CrashLogger(ILogger logger)
        {
            _logger = (logger ?? NullLogger.Instance).ForCategory("Crash");
        }

   
        public void Attach(Application application)
        {
            application.DispatcherUnhandledException += (sender, e) => LogUiThreadException(e.Exception);

            AppDomain.CurrentDomain.UnhandledException += (sender, e) => LogUnhandledException(e.ExceptionObject, e.IsTerminating);

            TaskScheduler.UnobservedTaskException += (sender, e) => LogUnobservedTaskException(e.Exception);
        }

        public void LogUiThreadException(Exception exception)
        {
            LogOnce(exception, "Unhandled exception on the UI thread; the application will close");
        }

        public void LogUnhandledException(object exceptionObject, bool isTerminating)
        {
            var exception = exceptionObject as Exception
                ?? new Exception("Non-exception object thrown: " + (exceptionObject ?? "null"));
            LogOnce(exception, "Unhandled exception" + (isTerminating ? "; the application will close" : string.Empty));
        }

        public void LogUnobservedTaskException(AggregateException exception)
        {
            LogOnce(exception, "A background task failed and its error was never observed");
        }

        private void LogOnce(Exception exception, string message)
        {
            lock (_gate)
            {
                if (exception != null && ReferenceEquals(exception, _lastLogged))
                {
                    return;
                }

                _lastLogged = exception;
            }

            _logger.Error(message, exception);
        }
    }
}
