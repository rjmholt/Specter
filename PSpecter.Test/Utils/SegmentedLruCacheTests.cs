#nullable disable

using Xunit;
using PSpecter.Utils;

namespace PSpecter.Test.Utils
{
    public class SegmentedLruCacheTests
    {
        [Fact]
        public void BasicSetAndGet()
        {
            var cache = new SegmentedLruCache<string, int>(capacity: 10);
            cache.Set("a", 1);

            Assert.True(cache.TryGet("a", out int value));
            Assert.Equal(1, value);
        }

        [Fact]
        public void MissReturnsDefault()
        {
            var cache = new SegmentedLruCache<string, int>(capacity: 10);

            Assert.False(cache.TryGet("missing", out int value));
            Assert.Equal(0, value);
        }

        [Fact]
        public void EvictsProbationWhenFull()
        {
            var cache = new SegmentedLruCache<string, int>(capacity: 4, probationRatio: 0.5);

            cache.Set("a", 1);
            cache.Set("b", 2);

            // Probation capacity is 2. Adding a third should evict 'a'.
            cache.Set("c", 3);

            Assert.False(cache.TryGet("a", out _));
            Assert.True(cache.TryGet("b", out _));
            Assert.True(cache.TryGet("c", out _));
        }

        [Fact]
        public void SecondAccessPromotesToProtected()
        {
            // capacity=4, probation=1 (25%), protected=3 (75%)
            var cache = new SegmentedLruCache<string, int>(capacity: 4, probationRatio: 0.25);

            cache.Set("a", 1);
            // First get promotes 'a' to protected
            Assert.True(cache.TryGet("a", out _));

            // Fill probation: capacity 1 entry
            cache.Set("b", 2);

            // 'a' should still be accessible (in protected)
            Assert.True(cache.TryGet("a", out int value));
            Assert.Equal(1, value);
        }

        [Fact]
        public void ProtectedItemSurvivesNewInsertions()
        {
            // capacity=4, probation=1, protected=3
            var cache = new SegmentedLruCache<string, int>(capacity: 4, probationRatio: 0.25);

            cache.Set("hot", 100);
            cache.TryGet("hot", out _); // promote to protected

            // Insert many items into probation to force evictions
            for (int i = 0; i < 20; i++)
            {
                cache.Set($"cold_{i}", i);
            }

            // 'hot' should survive in the protected segment
            Assert.True(cache.TryGet("hot", out int value));
            Assert.Equal(100, value);
        }

        [Fact]
        public void UpdateExistingValue()
        {
            var cache = new SegmentedLruCache<string, int>(capacity: 10);
            cache.Set("key", 1);
            cache.Set("key", 2);

            Assert.True(cache.TryGet("key", out int value));
            Assert.Equal(2, value);
        }

        [Fact]
        public void ClearRemovesAll()
        {
            var cache = new SegmentedLruCache<string, int>(capacity: 10);
            cache.Set("a", 1);
            cache.Set("b", 2);

            cache.Clear();

            Assert.Equal(0, cache.Count);
            Assert.False(cache.TryGet("a", out _));
            Assert.False(cache.TryGet("b", out _));
        }

        [Fact]
        public void CountTracksEntries()
        {
            var cache = new SegmentedLruCache<string, int>(capacity: 10);
            Assert.Equal(0, cache.Count);

            cache.Set("a", 1);
            Assert.Equal(1, cache.Count);

            cache.Set("b", 2);
            Assert.Equal(2, cache.Count);

            // Updating doesn't increase count
            cache.Set("a", 3);
            Assert.Equal(2, cache.Count);
        }

        [Fact]
        public void ProtectedDemotionCascades()
        {
            // capacity=3, probation=1, protected=2
            var cache = new SegmentedLruCache<string, int>(capacity: 3, probationRatio: 0.34);

            // Fill protected: add and promote two items
            cache.Set("p1", 1);
            cache.TryGet("p1", out _); // promoted

            cache.Set("p2", 2);
            cache.TryGet("p2", out _); // promoted

            // Protected is now full. Add and promote another.
            cache.Set("p3", 3);
            cache.TryGet("p3", out _); // promoted -> demotes p1 back to probation

            // p1 may or may not survive depending on probation evictions,
            // but p2 and p3 should be in protected.
            Assert.True(cache.TryGet("p2", out _));
            Assert.True(cache.TryGet("p3", out _));
        }

        [Fact]
        public void NullValuesCached()
        {
            var cache = new SegmentedLruCache<string, string>(capacity: 10);
            cache.Set("key", null);

            Assert.True(cache.TryGet("key", out string value));
            Assert.Null(value);
        }
    }
}
