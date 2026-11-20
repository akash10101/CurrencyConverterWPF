using System;
using System.Linq;
using Challenge.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Challenge.Tests
{
    [TestClass]
    public class CrashLoggerTests
    {
        [TestMethod]
        public void UiThreadCrash_IsLoggedAsAnErrorWithTheException()
        {
            var logger = new RecordingLogger();

            new CrashLogger(logger).LogUiThreadException(new InvalidOperationException("boom"));

            Assert.AreEqual(
                "Error [Crash] Unhandled exception on the UI thread; the application will close | InvalidOperationException",
                logger.Lines.Single());
        }

        [TestMethod]
        public void TheSameCrash_ReportedByDispatcherAndAppDomain_IsLoggedOnce()
        {
            var logger = new RecordingLogger();
            var crashLogger = new CrashLogger(logger);
            var exception = new InvalidOperationException("boom");

            crashLogger.LogUiThreadException(exception);
            crashLogger.LogUnhandledException(exception, true);

            Assert.AreEqual(1, logger.Lines.Count);
        }

        [TestMethod]
        public void BackgroundThreadCrash_AndNonExceptionObjects_AreLogged()
        {
            var logger = new RecordingLogger();
            var crashLogger = new CrashLogger(logger);

            crashLogger.LogUnhandledException(new ArgumentException("bad"), true);
            crashLogger.LogUnhandledException("a string was thrown", false);

            Assert.AreEqual("Error [Crash] Unhandled exception; the application will close | ArgumentException", logger.Lines[0]);
            Assert.AreEqual("Error [Crash] Unhandled exception | Exception", logger.Lines[1]);
        }

        [TestMethod]
        public void UnobservedTaskFailure_IsLogged()
        {
            var logger = new RecordingLogger();

            new CrashLogger(logger).LogUnobservedTaskException(new AggregateException(new TimeoutException()));

            Assert.AreEqual(
                "Error [Crash] A background task failed and its error was never observed | AggregateException",
                logger.Lines.Single());
        }
    }
}
