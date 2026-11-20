using System;

namespace Challenge.Caching
{
    /// <summary>A cached value together with the moment it was stored.</summary>
    public sealed class CacheEntry<TKey, TValue>
    {
        public CacheEntry(TKey key, TValue value, DateTime createdUtc)
        {
            Key = key;
            Value = value;
            CreatedUtc = createdUtc;
        }

        public TKey Key { get; }

        public TValue Value { get; }

        public DateTime CreatedUtc { get; }
    }
}
