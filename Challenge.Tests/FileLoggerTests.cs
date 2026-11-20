using System;
using System.IO;
using System.Linq;
using Challenge.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Challenge.Tests
{
    [TestClass]
    public class FileLoggerTests
    {
        private string _directory;

        [TestInitialize]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "CurrencyConverterLogTests", Guid.NewGuid().ToString("N"));
        }

        [TestCleanup]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        [TestMethod]
        public void Log_AppendsATimestampedLineToTheDailyFile()
        {
            var logger = new FileLogger(_directory, LogLevel.Info, () => new DateTime(2026, 11, 21, 9, 30, 15, 250));

            logger.ForCategory("Test").Info("hello");

            var path = Path.Combine(_directory, "app-20261121.log");
            Assert.AreEqual(path, logger.CurrentFilePath);
            var line = File.ReadAllLines(path).Single();
            StringAssert.StartsWith(line, "2026-11-21 09:30:15.250 [INFO   ]");
            StringAssert.EndsWith(line, "[Test] hello");
        }

        [TestMethod]
        public void Log_SkipsMessagesBelowTheMinimumLevel()
        {
            var logger = new FileLogger(_directory, LogLevel.Warning);

            logger.Info("not written");
            logger.Error("written", new InvalidOperationException("boom"));

            var text = File.ReadAllText(logger.CurrentFilePath);
            Assert.IsFalse(text.Contains("not written"));
            StringAssert.Contains(text, "[ERROR  ]");
            StringAssert.Contains(text, "System.InvalidOperationException: boom");
        }

        [TestMethod]
        public void Log_NeverThrows_WhenTheFileCannotBeWritten()
        {
            Directory.CreateDirectory(_directory);
            var logger = new FileLogger(_directory);

            using (File.Open(logger.CurrentFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                logger.Info("file is locked");
            }
        }
    }
}
