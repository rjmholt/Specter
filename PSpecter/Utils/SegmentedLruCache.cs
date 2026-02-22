#nullable disable

using System;
using System.Collections.Generic;

namespace PSpecter.Utils
{
    /// <summary>
    /// A two-segment (probation + protected) LRU cache that balances recency
    /// with frequency. New entries land in the probation segment; a second access
    /// promotes them to the protected segment. Eviction always comes from the
    /// probation segment first, protecting frequently-accessed entries from bursts
    /// of novel lookups.
    /// </summary>
    /// <remarks>
    /// All operations are O(1). Not thread-safe; callers must synchronize if
    /// accessed concurrently.
    /// </remarks>
    public sealed class SegmentedLruCache<TKey, TValue>
    {
        private readonly int _probationCapacity;
        private readonly int _protectedCapacity;

        private readonly Dictionary<TKey, LinkedListNode<CacheEntry>> _index;
        private readonly LinkedList<CacheEntry> _probation;
        private readonly LinkedList<CacheEntry> _protected;

        /// <param name="capacity">Total cache capacity. Default 1024.</param>
        /// <param name="probationRatio">
        /// Fraction of capacity allocated to probation (0.0-1.0). Default 0.2 (20%).
        /// </param>
        /// <param name="comparer">Key comparer. Defaults to <see cref="EqualityComparer{TKey}.Default"/>.</param>
        public SegmentedLruCache(
            int capacity = 1024,
            double probationRatio = 0.2,
            IEqualityComparer<TKey> comparer = null)
        {
            if (capacity < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be at least 2.");
            }

            if (probationRatio <= 0 || probationRatio >= 1)
            {
                throw new ArgumentOutOfRangeException(nameof(probationRatio), "Must be between 0 and 1 exclusive.");
            }

            _probationCapacity = Math.Max(1, (int)(capacity * probationRatio));
            _protectedCapacity = capacity - _probationCapacity;

            _index = new Dictionary<TKey, LinkedListNode<CacheEntry>>(capacity, comparer ?? EqualityComparer<TKey>.Default);
            _probation = new LinkedList<CacheEntry>();
            _protected = new LinkedList<CacheEntry>();
        }

        public int Count => _index.Count;

        /// <summary>
        /// Attempts to retrieve a cached value. On a hit, the entry is promoted
        /// (probation -> protected on second access, or moved to head of protected).
        /// </summary>
        public bool TryGet(TKey key, out TValue value)
        {
            if (!_index.TryGetValue(key, out LinkedListNode<CacheEntry> node))
            {
                value = default;
                return false;
            }

            CacheEntry entry = node.Value;

            if (entry.Segment == Segment.Probation)
            {
                _probation.Remove(node);
                entry.Segment = Segment.Protected;
                EvictFromProtectedIfNeeded();
                _protected.AddFirst(node);
            }
            else
            {
                _protected.Remove(node);
                _protected.AddFirst(node);
            }

            value = entry.Value;
            return true;
        }

        /// <summary>
        /// Adds or updates an entry. New entries go to probation; existing entries
        /// are treated as a hit (promoted/refreshed).
        /// </summary>
        public void Set(TKey key, TValue value)
        {
            if (_index.TryGetValue(key, out LinkedListNode<CacheEntry> existing))
            {
                existing.Value.Value = value;

                if (existing.Value.Segment == Segment.Probation)
                {
                    _probation.Remove(existing);
                    existing.Value.Segment = Segment.Protected;
                    EvictFromProtectedIfNeeded();
                    _protected.AddFirst(existing);
                }
                else
                {
                    _protected.Remove(existing);
                    _protected.AddFirst(existing);
                }

                return;
            }

            EvictFromProbationIfNeeded();

            var entry = new CacheEntry(key, value, Segment.Probation);
            var node = new LinkedListNode<CacheEntry>(entry);
            _probation.AddFirst(node);
            _index[key] = node;
        }

        /// <summary>Removes all entries from the cache.</summary>
        public void Clear()
        {
            _index.Clear();
            _probation.Clear();
            _protected.Clear();
        }

        private void EvictFromProbationIfNeeded()
        {
            while (_probation.Count >= _probationCapacity)
            {
                LinkedListNode<CacheEntry> victim = _probation.Last;
                _probation.RemoveLast();
                _index.Remove(victim.Value.Key);
            }
        }

        private void EvictFromProtectedIfNeeded()
        {
            while (_protected.Count >= _protectedCapacity)
            {
                LinkedListNode<CacheEntry> demoted = _protected.Last;
                _protected.RemoveLast();
                demoted.Value.Segment = Segment.Probation;
                _probation.AddFirst(demoted);

                EvictFromProbationIfNeeded();
            }
        }

        private enum Segment
        {
            Probation,
            Protected
        }

        private sealed class CacheEntry
        {
            public CacheEntry(TKey key, TValue value, Segment segment)
            {
                Key = key;
                Value = value;
                Segment = segment;
            }

            public TKey Key { get; }
            public TValue Value { get; set; }
            public Segment Segment { get; set; }
        }
    }
}
