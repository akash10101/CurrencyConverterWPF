using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Challenge.Caching;
using Challenge.Controllers;
using Challenge.Models;
using Challenge.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Challenge.Tests
{
    [TestClass]
    public class CachedConversionControllerTests
    {
        private static readonly DateTime Start = new DateTime(2025, 1, 1);
        private static readonly DateTime End = new DateTime(2025, 1, 31);

        private FakeConversionController _inner;

        private CachedConversionController Create(int maxElements = 10)
        {
            _inner = new FakeConversionController();
            var cache = new QueryCache<HistoricRatesQuery, IReadOnlyList<DailyRate>>(new SizeBasedEvictionStrategy(maxElements));
            return new CachedConversionController(_inner, cache);
        }

        [TestMethod]
        public async Task RepeatedHistoricQuery_IsAnsweredFromTheCache()
        {
            var controller = Create();

            var first = await controller.GetHistoricRatesAsync("EUR", "USD", Start, End, CancellationToken.None);
            var second = await controller.GetHistoricRatesAsync("EUR", "USD", Start, End, CancellationToken.None);

            Assert.AreEqual(1, _inner.HistoricCalls);
            Assert.AreSame(first, second);
        }

        [TestMethod]
        public async Task EquivalentQueries_ShareOneCacheEntry()
        {
            var controller = Create();

            await controller.GetHistoricRatesAsync("EUR", "USD", Start, End, CancellationToken.None);
            await controller.GetHistoricRatesAsync("eur", "usd", Start.AddHours(5), End.AddHours(7), CancellationToken.None);

            Assert.AreEqual(1, _inner.HistoricCalls);
        }

        [TestMethod]
        public async Task DifferentQueries_AreFetchedSeparately()
        {
            var controller = Create();

            await controller.GetHistoricRatesAsync("EUR", "USD", Start, End, CancellationToken.None);
            await controller.GetHistoricRatesAsync("EUR", "GBP", Start, End, CancellationToken.None);
            await controller.GetHistoricRatesAsync("EUR", "USD", Start, End.AddDays(-1), CancellationToken.None);

            Assert.AreEqual(3, _inner.HistoricCalls);
        }

        [TestMethod]
        public async Task EvictedQuery_IsFetchedAgain()
        {
            var controller = Create(maxElements: 1);

            await controller.GetHistoricRatesAsync("EUR", "USD", Start, End, CancellationToken.None);
            await controller.GetHistoricRatesAsync("EUR", "GBP", Start, End, CancellationToken.None);
            await controller.GetHistoricRatesAsync("EUR", "USD", Start, End, CancellationToken.None);

            Assert.AreEqual(3, _inner.HistoricCalls);
        }

        [TestMethod]
        public async Task FailedRequests_AreNotCached()
        {
            var controller = Create();
            _inner.HistoricException = new ExchangeRateException("down");

            await Assert.ThrowsExceptionAsync<ExchangeRateException>(
                () => controller.GetHistoricRatesAsync("EUR", "USD", Start, End, CancellationToken.None));

            _inner.HistoricException = null;
            await controller.GetHistoricRatesAsync("EUR", "USD", Start, End, CancellationToken.None);

            Assert.AreEqual(2, _inner.HistoricCalls);
        }

        [TestMethod]
        public async Task LatestRate_IsNeverCached()
        {
            var controller = Create();

            await controller.GetLatestRateAsync("EUR", "USD", CancellationToken.None);
            await controller.GetLatestRateAsync("EUR", "USD", CancellationToken.None);

            Assert.AreEqual(2, _inner.LatestCalls);
        }
    }
}
