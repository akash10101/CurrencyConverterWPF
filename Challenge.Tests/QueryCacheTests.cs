using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Challenge.Caching;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Challenge.Tests
{
    [TestClass]
    public class QueryCacheTests
    {
        private static QueryCache<string, int> TimeBasedCache(FakeClock clock, int maxAgeMinutes)
        {
            return new QueryCache<string, int>(new TimeBasedEvictionStrategy(TimeSpan.FromMinutes(maxAgeMinutes)), () => clock.UtcNow);
        }

        private static QueryCache<string, int> SizeBasedCache(int maxElements)
        {
            return new QueryCache<string, int>(new SizeBasedEvictionStrategy(maxElements));
        }

        [TestMethod]
        public void Get_MissesUntilSet_ThenHits()
        {
            var cache = SizeBasedCache(5);
            int value;

            Assert.IsFalse(cache.TryGet("a", out value));

            cache.Set("a", 1);

            Assert.IsTrue(cache.TryGet("a", out value));
            Assert.AreEqual(1, value);
        }

        [TestMethod]
        public void TimeBased_EntryIsServedUntilItIsOlderThanMaxAge()
        {
            var clock = new FakeClock();
            var cache = TimeBasedCache(clock, 10);
            cache.Set("a", 1);
            int value;

            clock.Advance(TimeSpan.FromMinutes(9));
            Assert.IsTrue(cache.TryGet("a", out value));

            clock.Advance(TimeSpan.FromMinutes(2));
            Assert.IsFalse(cache.TryGet("a", out value), "older than 10 minutes");
            Assert.AreEqual(0, cache.Count);
        }

        [TestMethod]
        public void TimeBased_UpdatingTheCacheRemovesExpiredEntries()
        {
            var clock = new FakeClock();
            var cache = TimeBasedCache(clock, 10);
            cache.Set("old", 1);

            clock.Advance(TimeSpan.FromMinutes(11));
            cache.Set("new", 2);

            Assert.AreEqual(1, cache.Count);
            int value;
            Assert.IsTrue(cache.TryGet("new", out value));
        }

        [TestMethod]
        public void TimeBased_StoringAKeyAgainRestartsItsAge()
        {
            var clock = new FakeClock();
            var cache = TimeBasedCache(clock, 10);
            cache.Set("a", 1);

            clock.Advance(TimeSpan.FromMinutes(8));
            cache.Set("a", 2);
            clock.Advance(TimeSpan.FromMinutes(8));

            int value;
            Assert.IsTrue(cache.TryGet("a", out value));
            Assert.AreEqual(2, value);
        }

        [TestMethod]
        public void SizeBased_RemovesTheOldestEntryWhenTheLimitIsExceeded()
        {
            var cache = SizeBasedCache(2);

            cache.Set("a", 1);
            cache.Set("b", 2);
            cache.Set("c", 3);

            int value;
            Assert.AreEqual(2, cache.Count);
            Assert.IsFalse(cache.TryGet("a", out value), "a is the oldest");
            Assert.IsTrue(cache.TryGet("b", out value));
            Assert.IsTrue(cache.TryGet("c", out value));
        }

        [TestMethod]
        public void SizeBased_StoringAKeyAgainMakesItTheNewest()
        {
            var cache = SizeBasedCache(2);

            cache.Set("a", 1);
            cache.Set("b", 2);
            cache.Set("a", 10);
            cache.Set("c", 3);

            int value;
            Assert.IsFalse(cache.TryGet("b", out value), "b became the oldest");
            Assert.IsTrue(cache.TryGet("a", out value));
            Assert.AreEqual(10, value);
        }

        [TestMethod]
        public void SizeBased_ReadingDoesNotChangeTheOrder()
        {
            var cache = SizeBasedCache(2);
            cache.Set("a", 1);
            cache.Set("b", 2);
            int value;

            cache.TryGet("a", out value);
            cache.Set("c", 3);

            Assert.IsFalse(cache.TryGet("a", out value), "eviction is by age of storage, not by last use");
        }

        [TestMethod]
        public void ConcurrentAccess_IsThreadSafeAndRespectsTheLimit()
        {
            var cache = SizeBasedCache(10);

            Parallel.For(0, 20000, i =>
            {
                var key = "k" + (i % 50);
                cache.Set(key, i);
                int value;
                cache.TryGet(key, out value);
            });

            Assert.IsTrue(cache.Count <= 10, "count was " + cache.Count);
        }

        [TestMethod]
        public void InvalidLimits_AreRejected()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SizeBasedEvictionStrategy(0));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new TimeBasedEvictionStrategy(TimeSpan.Zero));
        }

        [TestMethod]
        public void Strategies_SelectTheRightEntries()
        {
            var now = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var entries = new List<CacheEntry<string, int>>
            {
                new CacheEntry<string, int>("a", 1, now.AddMinutes(-30)),
                new CacheEntry<string, int>("b", 2, now.AddMinutes(-5)),
                new CacheEntry<string, int>("c", 3, now.AddMinutes(-1)),
            };

            var expired = new TimeBasedEvictionStrategy(TimeSpan.FromMinutes(10)).SelectEntriesToEvict<string, int>(entries, now);
            var excess = new SizeBasedEvictionStrategy(2).SelectEntriesToEvict<string, int>(entries, now);

            CollectionAssert.AreEqual(new[] { "a" }, new List<string>(System.Linq.Enumerable.Select(expired, e => e.Key)));
            CollectionAssert.AreEqual(new[] { "a" }, new List<string>(System.Linq.Enumerable.Select(excess, e => e.Key)));
        }
    }
}
