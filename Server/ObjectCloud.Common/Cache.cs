// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

using Common.Logging;

namespace ObjectCloud.Common
{
    /// <summary>
    /// Implements a cache of objects that can be garbage collected on an as-needed basis
    /// </summary>
    /// <typeparam name="TKey">The type that keys the cache</typeparam>
    /// <typeparam name="TValue">The type that is cached</typeparam>
    public class Cache<TKey, TValue> : Cache<TKey, TValue, object>
        where TValue : class
    {
        public Cache(CreateForCache<TKey, TValue> createForCache)
            : base(delegate(TKey key, object toDiscard)
        {
            return createForCache(key);
        }) { }

        /// <summary>
        /// Gets the corresponding object, constructs it if it hasn't been constructed or if it was garbage collected
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public TValue this[TKey key]
        {
            get { return Get(key, null); }
            set { base.Set(key, value); }
        }
    }

    /// <summary>
    /// Delegate for creating an object when the cache is stale
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <typeparam name="TConstructorArg"></typeparam>
    /// <param name="key"></param>
    /// <param name="constructorArg"></param>
    /// <returns></returns>
    public delegate TValue CreateForCache<TKey, TValue>(TKey key);

    /// <summary>
    /// Implements a cache of objects that can be garbage collected on an as-needed basis
    /// </summary>
    /// <typeparam name="TKey">The type that keys the cache</typeparam>
    /// <typeparam name="TValue">The type that is cached</typeparam>
    /// <typeparam name="TConstructorArg">The type needed to construct the cached value</typeparam>
    public class Cache<TKey, TValue, TConstructorArg> : Cache
        where TValue : class
    {
        private readonly Dictionary<TKey, CacheHandle> Dictionary =
            new Dictionary<TKey, CacheHandle>();

        protected readonly CreateForCache<TKey, TValue, TConstructorArg> _CreateForCache;

        public Cache(CreateForCache<TKey, TValue, TConstructorArg> createForCache)
        {
            _CreateForCache = createForCache;
        }

        /// <summary>
        /// Gets or creates a cache handle for the given key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private CacheHandle GetCacheHandle(TKey key)
        {
            // Get or create the cache handle
            CacheHandle cacheHandle;
            using (TimedLock.Lock(Dictionary))
                if (!Dictionary.TryGetValue(key, out cacheHandle))
                {
                    cacheHandle = new CacheHandle(this, key);
                    Dictionary[key] = cacheHandle;
                }

            return cacheHandle;
        }

        /// <summary>
        /// Gets the corresponding object, constructs it if it hasn't been constructed or if it was garbage collected
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public TValue Get(TKey key, TConstructorArg constructorArg)
        {
            CacheHandle cacheHandle = GetCacheHandle(key);
            return cacheHandle.GetValue(constructorArg);
        }

        /// <summary>
        /// Explicit setter
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Set(TKey key, TValue value)
        {
            CacheHandle cacheHandle = GetCacheHandle(key);
            cacheHandle.SetValue(value);
        }

        /// <summary>
        /// Removes the value with the given key from the dictionary
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Remove(TKey key)
        {
            // Get cache handle if it exists
            CacheHandle cacheHandle;
            using (TimedLock.Lock(Dictionary))
            {
                if (Dictionary.TryGetValue(key, out cacheHandle))
                    Dictionary.Remove(key);
                else
                    return false;

                TValue cached = cacheHandle.WeakValue;
                cacheHandle.SetValue(null);

                if (null == cached)
                    return false;

                if (cached is IDisposable)
                    ((IDisposable)cached).Dispose();
            }

            return true;
        }

        /// <summary>
        /// Clears the cache
        /// </summary>
        public void Clear()
        {
            using (TimedLock.Lock(Dictionary))
            {
                foreach (TValue value in Values)
                    if (value is IDisposable)
                        ((IDisposable)value).Dispose();

                Dictionary.Clear();
            }
        }

        /// <summary>
        /// Allows thread-safe enumeration over all values in the cache
        /// </summary>
        public IEnumerable<TValue> Values
        {
            get
            {
                List<CacheHandle> toEnumerate;

                using (TimedLock.Lock(Dictionary))
                    toEnumerate = new List<CacheHandle>(Dictionary.Values);

                foreach (CacheHandle cacheHandle in toEnumerate)
                {
                    TValue toYield = cacheHandle.WeakValue;

                    if (null != toYield)
                        yield return toYield;
                }
            }
        }

        /// <summary>
        /// Handle to an object that manages constructing it if needed
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        private class CacheHandle : ICacheHandle
        {
            /// <summary>
            /// The parent cache
            /// </summary>
            internal Cache<TKey, TValue, TConstructorArg> ParentCache;

            /// <summary>
            /// The key
            /// </summary>
            internal TKey Key;

            internal CacheHandle(Cache<TKey, TValue, TConstructorArg> parentCache, TKey key)
            {
                ParentCache = parentCache;
                Key = key;
            }

            /// <summary>
            /// Field that holds a reference to the cached value when it is to stay in RAM
            /// </summary>
            private TValue Value = null;

            /// <summary>
            /// The weak reference, this is used to prevent creating duplicate instances of an object in case it isn't collected after the cache decides to discard it
            /// </summary>
            private WeakReference WeakReference = new WeakReference(null);

            /// <summary>
            /// The cached value, or null if it's been garbage collected
            /// </summary>
            internal TValue WeakValue
            {
                get { return (TValue)WeakReference.Target; }
            }

            /// <summary>
            /// Helper to help the cache get rid of stale CacheHandles, returns false if the object is no longer cached and completely collected
            /// </summary>
            public bool RemoveIfNotAlive()
            {
                using (TimedLock.Lock(this))
                    if (NumAccesses > 0)
                        return true;

                if (!WeakReference.IsAlive)
                {
                    using (TimedLock.Lock(ParentCache.Dictionary))
                        ParentCache.Dictionary.Remove(Key);

                    return true;
                }

                return false;
            }

            /// <summary>
            /// The number of times this object has been accessed, minus the times the object was removed from the access queue.  When this is zero, the cache will no longer reference the object
            /// </summary>
            uint NumAccesses = 0;

            /// <summary>
            /// Increments the number of accesses to this object, thus helping fix its place in RAM
            /// </summary>
            public void IncrementAccesses()
            {
                using (TimedLock.Lock(this))
                    NumAccesses++;

                Cache.MonitorCacheHandle(this);
            }

            /// <summary>
            /// Decrements the number of accesses to this object, thus helping free it from in RAM
            /// </summary>
            /// <returns>True if the cache handle is no longer holding a strong reference</returns>
            public bool DecrementAccesses()
            {
                using (TimedLock.Lock(this))
                {
                    NumAccesses--;

                    if (NumAccesses <= 0)
                    {
                        Value = null;
                        return true;
                    }
                }

                return false;
            }

            public override bool Equals(object obj)
            {
                if (obj is CacheHandle)
                    return ((CacheHandle)obj).Key.Equals(Key);

                return false;
            }

            public override int GetHashCode()
            {
                return Key.GetHashCode();
            }

            /// <summary>
            /// Gets the value, constructing it if needed
            /// </summary>
            internal TValue GetValue(TConstructorArg constructorArg)
            {
                using (TimedLock.Lock(this))
                {
                    // If the value is known to be in the cache
                    if (NumAccesses > 0)
                    {
                        IncrementAccesses();
                        return Value;
                    }

                    // The value isn't in the cache, but if it's still in memory due to the weak reference, re-use it, else re-construct it

                    Value = (TValue)WeakReference.Target;

                    if (null == Value)
                    {
                        Value = ParentCache._CreateForCache(Key, constructorArg);
                        WeakReference.Target = Value;
                    }

                    IncrementAccesses();
                    return Value;
                }
            }

            /// <summary>
            /// Explicitly sets the value
            /// </summary>
            /// <param name="value"></param>
            internal void SetValue(TValue value)
            {
                using (TimedLock.Lock(this))
                {
                    Value = value;
                    WeakReference.Target = value;

                    if (null != value)
                        IncrementAccesses();

                    // There's no clean way to remove values from the reference cache...  We can just hope that this gets collected!
                }
            }
        }
    }

    /// <summary>
    /// Provides basic functionality for shared cache management
    /// </summary>
    public abstract class Cache
    {
        private static ILog log = LogManager.GetLogger<Cache>();

        internal Cache() { }

        /// <summary>
        /// Sets the percentage of the working set that ObjectCloud will attempt to work with.  This adjusts the default memory use parameters
        /// </summary>
        public static double PercentOfMaxWorkingSet
        {
            get { return Cache._PercentOfMaxWorkingSet; }
            set { Cache._PercentOfMaxWorkingSet = value; }
        }
        private static double _PercentOfMaxWorkingSet = 0.75;

        /// <summary>
        /// These values tune the cache with regard to when it will start de-referencing objects.  For each value in here, if the process takes more memory then the value, an object
        /// will be decremented.  The lowest and second-lowest numbers should be the ideal memory range for the process; and any higher numbers will encourage agressive releasing of memory
        /// If set to null, the cache will default is to stay between 45-60% of the MaxWorkingSet, and agressively de-reference above 64% of MaxWorkingSet
        /// </summary>
        public static IEnumerable<long> MemorySizeLimits
        {
            get { return Cache._MemorySizeLimits; }
            set { Cache._MemorySizeLimits = value; }
        }
        private static IEnumerable<long> _MemorySizeLimits = null;

        /// <summary>
        /// The maximum memory to occupy.  If the process occupies memory above this limit, a garbage collection will occur, and then the cache will be flushed.  If null, the default is the MaxWorkingSet.
        /// </summary>
        public static long? MaxMemory
        {
            get { return Cache._MaxMemory; }
            set { Cache._MaxMemory = value; }
        }
        private static long? _MaxMemory = null;

        /// <summary>
        /// The minimum cache references allowed, unless MaxMemory is hit
        /// </summary>
        public static int MinCacheReferences
        {
            get { return Cache._MinCacheReferences; }
            set { Cache._MinCacheReferences = value; }
        }
        private static int _MinCacheReferences = 0;

        /// <summary>
        /// The absolute maximum cache references allowed
        /// </summary>
        public static int MaxCacheReferences
        {
            get { return Cache._MaxCacheReferences; }
            set { Cache._MaxCacheReferences = value; }
        }
        private static int _MaxCacheReferences = int.MaxValue;

        /// <summary>
        /// A queue of all of the cached objects.  Weak references are used in case a cache instance removes an item, or in case a cache instance is de-referenced.
        /// </summary>
        private static Queue<WeakReference> CachedReferences = new Queue<WeakReference>();

        /// <summary>
        /// Adds the cache handle to the set of monitored cache handles in a non-blocking way
        /// </summary>
        /// <param name="toMonitor"></param>
        internal static void MonitorCacheHandle(ICacheHandle toMonitor)
        {
            DelegateQueue.QueueUserWorkItem(delegate(object state)
            {
                DoCacheQueue((ICacheHandle)state);
            },
            toMonitor);
        }

        /// <summary>
        /// Single thread for cleaning the cache
        /// </summary>
        private static DelegateQueue DelegateQueue = new DelegateQueue();

        /// <summary>
        /// The current process
        /// </summary>
        static private Process MyProcess = Process.GetCurrentProcess();

        /// <summary>
        /// The number of collections that had occured after cleaning up dead weak referecnes.  When there are more MaxGeneration collections, dead weak references will be cleaned
        /// </summary>
        private static int LastCleanCollectionCount = GC.CollectionCount(GC.MaxGeneration);

        /// <summary>
        /// Adds a cache handle to the queue, and removes any references if memory use is getting high
        /// </summary>
        /// <param name="cacheHandle"></param>
        private static void DoCacheQueue(ICacheHandle cacheHandle)
        {
            // TODO:  So, this really should be a doubly-linked list that always moves itself to the head once it's referenced.
            // It should somehow use weak references to the containing Cache object so it isn't kept alive.  The Cache object's finalizer could
            // remove all of its entries, thus making cleanup smoother.
            // Likewise, when switching to doubly-linked-lists, the system should be less likely to de-reference an object when
            // moving within the doubly-linked list; as opposed to creating a new reference.

            try
            {
                CachedReferences.Enqueue(new WeakReference(cacheHandle));

                // Get rid of any excess cahced references
                while (CachedReferences.Count > MaxCacheReferences)
                    Decrement(CachedReferences.Dequeue());

                double maxWorkingSet = PercentOfMaxWorkingSet * (Convert.ToDouble(MyProcess.MaxWorkingSet.ToInt64()) / 0.001024);

                long maxMemory = null != MaxMemory ? MaxMemory.Value : Convert.ToInt64(maxWorkingSet);

                long processMemorySize = GC.GetTotalMemory(false);

                // If too much memory is used, force a GC to get a better estimate
                if (processMemorySize >= maxMemory)
                {
                    processMemorySize = GC.GetTotalMemory(true);

                    if (processMemorySize >= maxMemory)
                    {
                        while (CachedReferences.Count > MaxCacheReferences)
                            Decrement(CachedReferences.Dequeue());

                        return;
                    }
                }

                IEnumerable<long> memorySizeLimits;

                if (null != MemorySizeLimits)
                    memorySizeLimits = MemorySizeLimits;
                else
                    memorySizeLimits = new long[]
                    {
                        Convert.ToInt64(maxWorkingSet * 0.6),
                        Convert.ToInt64(maxWorkingSet * 0.8),
                        Convert.ToInt64(maxWorkingSet * 0.85),
                        Convert.ToInt64(maxWorkingSet * 0.9),
                        Convert.ToInt64(maxWorkingSet * 0.95)
                    };

                foreach (long memoryLevel in memorySizeLimits)
                    if (processMemorySize >= memoryLevel)

                        // Don't clean if we haven't reached the minimum
                        if (CachedReferences.Count > MinCacheReferences)
                            Decrement(CachedReferences.Dequeue());

                // If the garbage collector has run, clean up dead weak references
                while (LastCleanCollectionCount != GC.CollectionCount(GC.MaxGeneration))
                    MonitorHandlesForGC();
            }
            catch (Exception e)
            {
                log.Error("Error in the RAM cache!!!", e);
            }
        }

        /// <summary>
        /// Decrements a reference to a cache handle
        /// </summary>
        /// <param name="cacheHandleWR"></param>
        private static void Decrement(WeakReference cacheHandleWR)
        {
            ICacheHandle cacheHandle = (ICacheHandle)cacheHandleWR;

            if (null != cacheHandle)
            {
                if (cacheHandle.DecrementAccesses())
                    HandlesPendingGC.Add(new WeakReference(cacheHandle));
            }
            else
                // If a cache handle has been garbage collected, clean out all dead weak references
                MonitorHandlesForGC();
        }

        /// <summary>
        /// All of the handles that are waiting for a GC
        /// </summary>
        private static Set<WeakReference> HandlesPendingGC = new Set<WeakReference>();

        /// <summary>
        /// Monitors all of the handles to see if they've been GCed, if so, they are removed
        /// </summary>
        /// <param name="state"></param>
        private static void MonitorHandlesForGC()
        {
            try
            {
                LastCleanCollectionCount = GC.CollectionCount(GC.MaxGeneration);

                // If a cache handle is no longer in memory, it means that a cache was most likely collected
                // The entire queue of cache handles should be examined
                for (int ctr = 0; ctr < CachedReferences.Count; ctr++)
                {
                    WeakReference toCheck = CachedReferences.Dequeue();
                    if (toCheck.IsAlive)
                        CachedReferences.Enqueue(toCheck);
                }

                IEnumerable<WeakReference> toEnumerate = new LinkedList<WeakReference>(HandlesPendingGC);

                foreach (WeakReference cacheHandleWR in toEnumerate)
                {
                    ICacheHandle cacheHandle = (ICacheHandle)cacheHandleWR.Target;

                    if (null != cacheHandle)
                    {
                        if (cacheHandle.RemoveIfNotAlive())
                            HandlesPendingGC.Remove(cacheHandleWR);
                    }
                    else
                        HandlesPendingGC.Remove(cacheHandleWR);
                }
            }
            catch (Exception e)
            {
                log.Error("Error cleaning out old handles", e);
            }
        }
    }

    /// <summary>
    /// Interface that allows a generic CacheHandle to utilize a common cleanup system
    /// </summary>
    internal interface ICacheHandle
    {
        /// <summary>
        /// Decrements the number of accesses to this object, thus helping free it from in RAM
        /// </summary>
        /// <returns>True if the cache handle is no longer holding a strong reference</returns>
        bool DecrementAccesses();

        /// <summary>
        /// Instructs the CacheHandle to remove itself from its parent cache if its object is no longer cached and it's been garbage collected
        /// </summary>
        /// <returns>True if monitoring should stop</returns>
        bool RemoveIfNotAlive();
    }

    /// <summary>
    /// Delegate for creating an object when the cache is stale
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <typeparam name="TConstructorArg"></typeparam>
    /// <param name="key"></param>
    /// <param name="constructorArg"></param>
    /// <returns></returns>
    public delegate TValue CreateForCache<TKey, TValue, TConstructorArg>(TKey key, TConstructorArg constructorArg);
}