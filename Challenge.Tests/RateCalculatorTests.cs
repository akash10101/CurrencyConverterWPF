using System;
using System.Linq;
using Challenge.Models;
using Challenge.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Challenge.Tests
{
    [TestClass]
    public class RateCalculatorTests
    {
        private static readonly RateSnapshot Rates = new RateSnapshot(
            "USD",
            new DailyRate(new DateTime(2025, 1, 3), 1.5m),
            new[]
            {
                new DailyRate(new DateTime(2025, 1, 1), 1.2m),
                new DailyRate(new DateTime(2025, 1, 3), 1.5m),
                new DailyRate(new DateTime(2025, 1, 2), 1.4m)
            });

        [TestMethod]
        public void Convert_UsesTheLatestRateForTheMainResult()
        {
            var result = RateCalculator.Convert(Rates, 10m);

            Assert.AreEqual(15m, result.LatestAmount);
            Assert.AreEqual(1.5m, result.LatestRate);
            Assert.AreEqual(new DateTime(2025, 1, 3), result.LatestDate);
            Assert.AreEqual("USD", result.TargetCode);
        }

        [TestMethod]
        public void Convert_ConvertsEveryDay_NewestFirst()
        {
            var result = RateCalculator.Convert(Rates, 10m);

            CollectionAssert.AreEqual(
                new[] { new DateTime(2025, 1, 3), new DateTime(2025, 1, 2), new DateTime(2025, 1, 1) },
                result.History.Select(item => item.Date).ToArray());
            CollectionAssert.AreEqual(new[] { 15m, 14m, 12m }, result.History.Select(item => item.Value).ToArray());
            Assert.IsTrue(result.History.All(item => item.Currency == "USD"));
        }

        [TestMethod]
        public void Convert_KeepsFullDecimalPrecision()
        {
            var rates = new RateSnapshot("JPY", new DailyRate(new DateTime(2025, 1, 3), 161.234567m), new DailyRate[0]);

            Assert.AreEqual(19348.14804m, RateCalculator.Convert(rates, 120m).LatestAmount);
        }

        [TestMethod]
        public void Convert_WithNoHistory_ReturnsAnEmptyList()
        {
            var rates = new RateSnapshot("USD", new DailyRate(new DateTime(2025, 1, 3), 1.5m), new DailyRate[0]);

            Assert.AreEqual(0, RateCalculator.Convert(rates, 1m).History.Count);
        }

        [TestMethod]
        public void Convert_WithoutRates_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => RateCalculator.Convert(null, 1m));
        }
    }
}
