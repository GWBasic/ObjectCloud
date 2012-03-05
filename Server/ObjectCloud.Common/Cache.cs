// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

using Common.Logging;

using ObjectCloud.Common.Threading;

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
        /// <summary>
        /// Holds all local references
        /// </summary>
        private readonly Dictionary<TKey, WeakReference> Dictionary =
            new Dictionary<TKey, WeakReference>();

        private ReaderWriterLockSlim Lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        protected readonly CreateForCache<TKey, TValue, TConstructorArg> _CreateForCache;

        /// <summary>
        /// The GC count that was present at the last cleanup, used to prevent duplicate cleanups
        /// </summary> 
        private int _GCCountAtLastCleanup = GC.CollectionCount(GC.MaxGeneration);

        /// <summary>
        /// All of the weak references to periodically scan to see if the cache needs to be cleaned up
        /// </summary>
        private List<WeakReference> WRsToScan = new List<WeakReference>();

        /// <summary>
        /// The next WeakReference to scan
        /// </summary>
        private int ScanCtr = int.MinValue;

        public Cache(CreateForCache<TKey, TValue, TConstructorArg> createForCache)
        {
            _CreateForCache = createForCache;
        }

#if DEBUG
        /// <summary>
        /// Allows the cache to be turned off in debug builds
        /// </summary>
        public bool Enabled
        {
            get { return _Enabled; }
            set { _Enabled = value; }
        }
        private bool _Enabled = true;
#endif

        /// <summary>
        /// Checks an item in the cache to see if it's been collected, and enqueues a full cleanup if needed. Note: there must be at least an active read lock when calling this method
        /// </summary>
        private void CheckForCleanup()
        {
            List<WeakReference> wrsToScan = WRsToScan;

            if (0 == wrsToScan.Count)
                return;

            int scanCtr = Interlocked.Increment(ref ScanCtr) % wrsToScan.Count;
            WeakReference wr = wrsToScan[Math.Abs(scanCtr)];

            if (!wr.IsAlive)
                ThreadPool.QueueUserWorkItem(DoCleanup);
        }

        private void DoCleanup(object state)
        {
            Lock.EnterWriteLock();

            try
            {
                // Keep scanning while GCs are running
                while (_GCCountAtLastCleanup != GC.CollectionCount(GC.MaxGeneration))
                {
                    _GCCountAtLastCleanup = GC.CollectionCount(GC.MaxGeneration);

                    foreach (KeyValuePair<TKey, WeakReference> kvp in 
                        new List<KeyValuePair<TKey, WeakReference>>(Dictionary))
                    {
                        TKey key = kvp.Key;
                        WeakReference wr = kvp.Value;

                        if (!wr.IsAlive)
                            Dictionary.Remove(key);
                    }
                }

                WRsToScan.Clear();
                WRsToScan.AddRange(Dictionary.Values);
            }
            finally
            {
                Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Gets the corresponding object, constructs it if it hasn't been constructed or if it was garbage collected
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public TValue Get(TKey key, TConstructorArg constructorArg)
        {
#if DEBUG
            if (!Enabled)
                return _CreateForCache(key, constructorArg);

#endif
            WeakReference weakReference = null;

            object toReturn = null;

            Lock.EnterReadLock();
            try
            {
                CheckForCleanup();

                if (Dictionary.TryGetValue(key, out weakReference))
                    toReturn = weakReference.Target;
            }
            finally
            {
                Lock.ExitReadLock();
            }

            if (null != toReturn)
            {
                CacheObject(toReturn);
                return (TValue)toReturn;
            }

            // Value not in cache, or not alive, try to construct it

            // First make sure there's a weak reference in the cache
            if (null == weakReference)
            {
                Lock.EnterWriteLock();
                try
                {
                    if (!Dictionary.TryGetValue(key, out weakReference))
                    {
                        weakReference = new WeakReference(null);
                        Dictionary[key] = weakReference;

                        WRsToScan.Add(weakReference);
                    }
                }
                finally
                {
                    Lock.ExitWriteLock();
                }
            }

            // once there's a weak reference in the cache, use it for syncronization
            lock (weakReference)
            {
                toReturn = weakReference.Target;

                if (null == toReturn)
                {
                    toReturn = this._CreateForCache(key, constructorArg);
                    weakReference.Target = toReturn;
                }
            }

            CacheObject(toReturn);
            return (TValue)toReturn;
        }

        /// <summary>
        /// Explicit setter
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Set(TKey key, TValue value)
        {
            CacheObject(value);

            Lock.EnterUpgradeableReadLock();

            try
            {
                CheckForCleanup();

                WeakReference weakReference;
                if (Dictionary.TryGetValue(key, out weakReference))
                    weakReference.Target = value;
                else
                {
                    Lock.EnterWriteLock();

                    try
                    {
                        if (Dictionary.TryGetValue(key, out weakReference))
                            weakReference.Target = value;
                        else
                        {
                            weakReference = new WeakReference(value);
                            Dictionary[key] = weakReference;

                            WRsToScan.Add(weakReference);
                        }
                    }
                    finally
                    {
                        Lock.ExitWriteLock();
                    }
                }
            }
            finally
            {
                Lock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Removes the value with the given key from the dictionary
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Remove(TKey key)
        {
            // Get the weak reference if it exists
            WeakReference weakReference = null;

            Lock.EnterWriteLock();
            try
            {
                CheckForCleanup();

                if (!Dictionary.TryGetValue(key, out weakReference))
                    return false;
                
                Dictionary.Remove(key);
                WRsToScan.Remove(weakReference);
            }
            finally
            {
                Lock.ExitWriteLock();
            }

            object removed = weakReference.Target;

            if (null == removed)
                return true;

            if (removed is IDisposable)
                ((IDisposable)removed).Dispose();

            return true;
        }

        /// <summary>
        /// Clears the cache
        /// </summary>
        public void Clear()
        {
            Lock.EnterWriteLock();
            try
            {
                foreach (WeakReference weakReference in Dictionary.Values)
                {
                    object value = weakReference.Target;

                    if (null != value)
                    {
                        if (value is IDisposable)
                            ((IDisposable)value).Dispose();
                    }
                }

                Dictionary.Clear();
                WRsToScan.Clear();
            }
            finally
            {
                Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Allows thread-safe enumeration over all values in the cache
        /// </summary>
        public IEnumerable<TValue> Values
        {
            get
            {
                IEnumerable<WeakReference> toEnumerate;

                Lock.EnterReadLock();
                try
                {
                    toEnumerate = Enumerable<WeakReference>.FastCopy(Dictionary.Values);
                }
                finally
                {
                    Lock.ExitReadLock();
                }

                bool queuedCleanup = false;
                foreach (WeakReference weakReference in toEnumerate)
                {
                    object toYield = weakReference.Target;

                    if (null != toYield)
                        yield return (TValue)toYield;

                    // This is done instead of calling CheckForCleanup because each WR is checked
                    else if (!queuedCleanup)
                    {
                        queuedCleanup = true;
                        ThreadPool.QueueUserWorkItem(DoCleanup);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Provides basic functionality for shared cache management
    /// </summary>
    public abstract class Cache
    {
        internal static ILog log = LogManager.GetLogger<Cache>();

        public class Exception : System.Exception
        {
            internal Exception(string message) : base(message) { }
            internal Exception(string message, Exception inner) : base(message, inner) { }
        }

        /// <summary>
        /// All of the cache handles that are alive
        /// </summary>
        private static object[] CachedObjects = null;

        /// <summary>
        /// Counter that helps determine which element in the CacheHandles array is used for each cache hit
        /// </summary>
        private static int CacheHandlesCtr = int.MinValue;

        /// <summary>
        /// The number of references that the cache should keep. 20,000 is reccomended because the cache will keep multiple references to the same object when it is used many times
        /// </summary>
        public static int? CacheSize
        {
            get 
            {
                if (null == CachedObjects)
                    return null;

                return CachedObjects.Length; 
            }
            set
            {
                // Duplicate values can cause CPU load
                if (value == CacheSize)
                    return;

                if (null == value)
                    CachedObjects = null;
                else if (null == CachedObjects)
                    CachedObjects = new object[value.Value];
                else
                    Array.Resize<object>(ref CachedObjects, value.Value);
            }
        }

        public static void CacheObject(object toCache)
        {
            object[] cachedObjects = CachedObjects;

            if (null == cachedObjects)
                throw new Exception("CacheSize not set, reccomended size: 20,000");

            // Keep trying until this is the thread that overwrites
            int cacheHandlesCtr = Math.Abs(Interlocked.Increment(ref CacheHandlesCtr)) % cachedObjects.Length;
            cachedObjects[cacheHandlesCtr] = toCache;
        }

        /// <summary>
        /// Releases references to all cached objects, although objects will still be abailable while there are existing hard references and until a GC runs
        /// </summary>
        public static void ReleaseAllCachedMemory()
        {
            object[] cachedObjects = CachedObjects;

            if (null == cachedObjects)
                throw new Exception("CacheSize not set, reccomended size: 20,000");

            Array.Clear(cachedObjects, 0, cachedObjects.Length);
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
    public delegate TValue CreateForCache<TKey, TValue, TConstructorArg>(TKey key, TConstructorArg constructorArg);
}