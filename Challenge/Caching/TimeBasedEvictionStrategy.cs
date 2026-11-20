using System;
using System.Collections.Generic;
using System.Linq;

namespace Challenge.Caching
{
    /// <summary>Removes every entry that is older than MAX_AGE.</summary>
    public sealed class TimeBasedEvictionStrategy : IEvictionStrategy
    {
        private readonly TimeSpan _maxAge;

        public TimeBasedEvictionStrategy(TimeSpan maxAge)
        {
            if (maxAge <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAge), "The maximum age must be positive.");
            }

            _maxAge = maxAge;
        }

        public IReadOnlyCollection<CacheEntry<TKey, TValue>> SelectEntriesToEvict<TKey, TValue>(
            IReadOnlyList<CacheEntry<TKey, TValue>> entriesOldestFirst,
            DateTime utcNow)
        {
            return entriesOldestFirst.Where(entry => utcNow - entry.CreatedUtc > _maxAge).ToList();
        }
    }
}
