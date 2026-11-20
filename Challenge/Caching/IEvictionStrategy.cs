using System;
using System.Collections.Generic;

namespace Challenge.Caching
{
    /// <summary>Decides which cache entries have to go. The cache calls it after every update.</summary>
    public interface IEvictionStrategy
    {
        /// <param name="entriesOldestFirst">All current entries, ordered from the oldest to the newest.</param>
        /// <param name="utcNow">The current time, supplied by the cache so strategies stay deterministic and testable.</param>
        /// <returns>The entries that must be removed from the cache.</returns>
        IReadOnlyCollection<CacheEntry<TKey, TValue>> SelectEntriesToEvict<TKey, TValue>(
            IReadOnlyList<CacheEntry<TKey, TValue>> entriesOldestFirst,
            DateTime utcNow);
    }
}
