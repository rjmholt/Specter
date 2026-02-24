using System;
using System.Collections.Generic;

#if !CORECLR
using System.Collections.Concurrent;
#endif

namespace Specter.Internal
{
    public static class Polyfill
    {
#if !CORECLR
        private static ConcurrentDictionary<Type, Array> s_emptyArrays = new ConcurrentDictionary<Type, Array>();
#endif

        public static T[] GetEmptyArray<T>()
        {
#if CORECLR
            return Array.Empty<T>();
#else
            return (T[])s_emptyArrays.GetOrAdd(typeof(T), (_) => new T[0]);
#endif
        }

        /// <summary>
        /// Creates a shallow copy of a dictionary. On .NET Core this uses the copy constructor;
        /// on .NET Framework 4.6.2 it copies entries manually since the
        /// <c>Dictionary(IDictionary, IEqualityComparer)</c> overload accepting
        /// <see cref="IReadOnlyDictionary{TKey,TValue}"/> is not available.
        /// </summary>
        public static Dictionary<TKey, TValue> CopyDictionary<TKey, TValue>(
            IReadOnlyDictionary<TKey, TValue> source,
            IEqualityComparer<TKey>? comparer = null)
            where TKey : notnull
        {
            var copy = comparer != null
                ? new Dictionary<TKey, TValue>(source.Count, comparer)
                : new Dictionary<TKey, TValue>(source.Count);

            foreach (var kvp in source)
            {
                copy[kvp.Key] = kvp.Value;
            }

            return copy;
        }
    }
}

namespace System.Runtime.CompilerServices
{
        internal static class IsExternalInit { }
}
