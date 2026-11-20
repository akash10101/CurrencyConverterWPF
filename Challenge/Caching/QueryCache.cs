using System;
using System.Collections.Generic;
using System.Linq;

namespace Challenge.Caching
{
    /// <summary>
    /// A thread-safe cache that is cleaned up by an <see cref="IEvictionStrategy"/> every time it is updated.
    ///
    /// "Age" always means the time since an entry was stored (not since it was last read), and storing a key
    /// that already exists replaces the entry and makes it the newest one. Entries are kept in insertion
    /// order, so "the oldest entry" is unambiguous even when two entries get the same timestamp.
    /// A single lock guards all state; the operations are tiny, so contention is irrelevant here.
    /// </summary>
    public sealed class QueryCache<TKey, TValue>
    {
        private readonly object _sync = new object();
        private readonly Dictionary<TKey, LinkedListNode<CacheEntry<TKey, TValue>>> _nodesByKey;
        private readonly LinkedList<CacheEntry<TKey, TValue>> _entriesOldestFirst = new LinkedList<CacheEntry<TKey, TValue>>();
        private readonly IEvictionStrategy _evictionStrategy;
        private readonly Func<DateTime> _utcNow;

        public QueryCache(IEvictionStrategy evictionStrategy, Func<DateTime> utcNow = null, IEqualityComparer<TKey> keyComparer = null)
        {
            _evictionStrategy = evictionStrategy ?? throw new ArgumentNullException(nameof(evictionStrategy));
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _nodesByKey = new Dictionary<TKey, LinkedListNode<CacheEntry<TKey, TValue>>>(keyComparer);
        }

        public int Count
        {
            get
            {
                lock (_sync)
                {
                    return _nodesByKey.Count;
                }
            }
        }

        public bool TryGet(TKey key, out TValue value)
        {
            lock (_sync)
            {
                // The cache is only modified by updates, but a time-based strategy must not serve an entry that has
                // outlived MAX_AGE just because nothing was stored since. Cleaning up here is therefore part of reading.
                RemoveEvictedEntries();

                LinkedListNode<CacheEntry<TKey, TValue>> node;
                if (_nodesByKey.TryGetValue(key, out node))
                {
                    value = node.Value.Value;
                    return true;
                }

                value = default(TValue);
                return false;
            }
        }

        public void Set(TKey key, TValue value)
        {
            lock (_sync)
            {
                LinkedListNode<CacheEntry<TKey, TValue>> existing;
                if (_nodesByKey.TryGetValue(key, out existing))
                {
                    _entriesOldestFirst.Remove(existing);
                }

                _nodesByKey[key] = _entriesOldestFirst.AddLast(new CacheEntry<TKey, TValue>(key, value, _utcNow()));

                RemoveEvictedEntries();
            }
        }

        // Must be called while holding _sync.
        private void RemoveEvictedEntries()
        {
            if (_entriesOldestFirst.Count == 0)
            {
                return;
            }

            var snapshot = _entriesOldestFirst.ToList();
            foreach (var entry in _evictionStrategy.SelectEntriesToEvict<TKey, TValue>(snapshot, _utcNow()))
            {
                LinkedListNode<CacheEntry<TKey, TValue>> node;
                if (_nodesByKey.TryGetValue(entry.Key, out node))
                {
                    _entriesOldestFirst.Remove(node);
                    _nodesByKey.Remove(entry.Key);
                }
            }
        }
    }
}
