using System;
using System.Collections.Generic;
using System.Linq;

namespace Challenge.Caching
{
    /// <summary>Keeps at most MAX_ELEMENTS entries by removing the oldest ones.</summary>
    public sealed class SizeBasedEvictionStrategy : IEvictionStrategy
    {
        private readonly int _maxElements;

        public SizeBasedEvictionStrategy(int maxElements)
        {
            if (maxElements < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxElements), "The cache must be allowed to hold at least one element.");
            }

            _maxElements = maxElements;
        }

        public IReadOnlyCollection<CacheEntry<TKey, TValue>> SelectEntriesToEvict<TKey, TValue>(
            IReadOnlyList<CacheEntry<TKey, TValue>> entriesOldestFirst,
            DateTime utcNow)
        {
            var excess = entriesOldestFirst.Count - _maxElements;

            // Normally exactly one entry (the cache grows by one per update); taking "the excess" also
            // copes with a limit that was lowered or with several entries added at once.
            return excess > 0
                ? entriesOldestFirst.Take(excess).ToList()
                : new List<CacheEntry<TKey, TValue>>();
        }
    }
}
