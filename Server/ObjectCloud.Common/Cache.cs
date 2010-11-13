// Copyright 2009, 2010 Andrew Rondeau
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
        private readonly Dictionary<TKey, WeakReference> Dictionary =
            new Dictionary<TKey, WeakReference>();

        private ReaderWriterLockSlim Lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        protected readonly CreateForCache<TKey, TValue, TConstructorArg> _CreateForCache;

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
		/// This little bit checks a cache handle to see if it's alive, and if it's not, deletes it
        /// Basicaly, every time the cache is hit, some randome cache handle is checked to see if it's dead
		/// </summary>
        protected override void RemoveCollectedCacheHandles()
        {
            try
            {
                // First, figure out which items might be ready for removal

                LinkedList<TKey> toRemove = new LinkedList<TKey>();

                Lock.EnterReadLock();
                try
                {
                    foreach (KeyValuePair<TKey, WeakReference> kvp in Dictionary)
                        if (!(kvp.Value.IsAlive))
                            toRemove.AddLast(kvp.Key);
                }
                finally
                {
                    Lock.ExitReadLock();
                }

                // Next, remove them
                if (toRemove.Count > 0)
                {
                    Lock.EnterWriteLock();

                    try
                    {
                        foreach (TKey key in toRemove)
                            Dictionary.Remove(key);
                    }
                    finally
                    {
                        Lock.ExitWriteLock();
                    }
                }
			}
            catch (Exception e)
            {
                log.Warn("Exception when cleaning cache", e);
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
                WeakReference weakReference;
                if (Dictionary.TryGetValue(key, out weakReference))
                {
                    object toUnCache = weakReference.Target;

                    if (null != toUnCache)
                        UncacheObject(toUnCache);

                    weakReference.Target = value;
                }
                else
                {
                    Lock.EnterWriteLock();

                    try
                    {
                        if (Dictionary.TryGetValue(key, out weakReference))
                        {
                            object toUnCache = weakReference.Target;

                            if (null != toUnCache)
                                UncacheObject(toUnCache);

                            weakReference.Target = value;
                        }
                        else
                            Dictionary[key] = new WeakReference(value);
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
                if (!Dictionary.TryGetValue(key, out weakReference))
                    return false;
                
                Dictionary.Remove(key);
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

            UncacheObject(removed);

            return true;
        }

        /// <summary>
        /// Clears the cache
        /// </summary>
        public void Clear()
        {
            HashSet<object> toRemove = new HashSet<object>();

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

                        toRemove.Add(value);
                    }
                }

                Dictionary.Clear();
            }
            finally
            {
                Lock.ExitWriteLock();
            }

            UncacheObjects(toRemove);
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

                foreach (WeakReference weakReference in toEnumerate)
                {
                    object toYield = weakReference.Target;

                    if (null != toYield)
                        yield return (TValue)toYield;
                }
            }
        }

        /// <summary>
        /// If the cache is GCed, then it'll attempt to remove its cache handles from the cache
        /// </summary>
        ~Cache()
        {
            try
            {
                UncacheObjects(Dictionary.Values);
            }
            catch { }
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

        internal Cache()
        {
            AllCaches.Enqueue(new WeakReference(this));
        }

        /// <summary>
        /// Weak references to all in-memory caches
        /// </summary>
        protected static LockFreeQueue<WeakReference> AllCaches = new LockFreeQueue<WeakReference>();

        /// <summary>
        /// Used to track when to clean out old cached
        /// </summary>
        private static int PriorGCCollectionCount = 0;

        /// <summary>
        /// All of the cache handles that are alive
        /// </summary>
        private static object[] CachedObjects = null;

        /// <summary>
        /// Counter that helps determine which element in the CacheHandles array is used for each cache hit
        /// </summary>
        private static long CacheHandlesCtr = long.MinValue;

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

                object[] oldCacheHandles = CachedObjects;

                if (null == value)
                    CachedObjects = null;
                else
                    CachedObjects = new object[value.Value];

                // If there are old cache handles, spin off a thread that deallocates them
                if (null != oldCacheHandles)
                    ThreadPool.QueueUserWorkItem(delegate(object state)
                        {
                            for (int ctr = 0; ctr < oldCacheHandles.Length; ctr++)
                                try
                                {
                                    object cached = oldCacheHandles[ctr];

                                    if (cached is IAware)
                                        ((IAware)cached).DecrementCacheCount();
                                }
                                catch (Exception e)
                                {
                                    log.Warn("Exception while cleaning out old cached objects after resizing the cache", e);
                                }
                        });
            }
        }

        public static void CacheObject(object toCache)
        {
            object[] cachedObjects = CachedObjects;

            if (null == cachedObjects)
                throw new Exception("CacheSize not set, reccomended size: 20,000");

            long cacheHandlesCtr = Math.Abs(Interlocked.Increment(ref CacheHandlesCtr)) % cachedObjects.Length;

            object replaced = cachedObjects[cacheHandlesCtr];

            if (replaced is IAware)
            {
                // Only de-reference replaced if this is the thread that overwrites it
                if (replaced == Interlocked.CompareExchange<object>(ref cachedObjects[cacheHandlesCtr], toCache, replaced))
                    ((IAware)replaced).DecrementCacheCount();
            }
            else
                cachedObjects[cacheHandlesCtr] = toCache;

            if (toCache is IAware)
                ((IAware)toCache).IncrementCacheCount();

            CheckNeedCleanCaches();
        }

        public static void UncacheObject(object toRemove)
        {
            object[] cachedObjects = CachedObjects;

            if (null == cachedObjects)
                throw new Exception("CacheSize not set, reccomended size: 20,000");

            if (toRemove is IAware)
            {
                IAware casted = (IAware)toRemove;

                for (int ctr = 0; ctr < cachedObjects.Length; ctr++)
                    if (toRemove == Interlocked.CompareExchange<object>(ref cachedObjects[ctr], null, toRemove))
                        casted.DecrementCacheCount();
            }
            else
                for (int ctr = 0; ctr < cachedObjects.Length; ctr++)
                    Interlocked.CompareExchange<object>(ref cachedObjects[ctr], null, toRemove);

            CheckNeedCleanCaches();
        }

        public static void UncacheObjects(System.Collections.IEnumerable objectsToRemove)
        {
            object[] cachedObjects = CachedObjects;

            if (null == cachedObjects)
                throw new Exception("CacheSize not set, reccomended size: 20,000");

            HashSet<object> setToRemove = objectsToRemove as HashSet<object>;
            if (null == setToRemove)
            {
                if (objectsToRemove is IEnumerable<object>)
                    setToRemove.UnionWith((IEnumerable<object>)objectsToRemove);
                else
                    setToRemove.UnionWith(Enumerable<object>.Cast(objectsToRemove));
            }

            for (int ctr = 0; ctr < cachedObjects.Length; ctr++)
            {
                object cached = cachedObjects[ctr];

                if (setToRemove.Contains(cached))
                    if (cached == Interlocked.CompareExchange<object>(ref cachedObjects[ctr], null, cached))
                        if (cached is IAware)
                            ((IAware)cached).DecrementCacheCount();
            }

            CheckNeedCleanCaches();
        }

        /// <summary>
        /// Clears all in-memory cached objects, except those which have strong references elsewhere
        /// </summary>
        public static void ReleaseAllCachedMemory()
        {
            object[] cachedObjects = CachedObjects;

            if (null == cachedObjects)
                throw new Exception("CacheSize not set, reccomended size: 20,000");

            for (int ctr = 0; ctr < cachedObjects.Length; ctr++)
            {
                object cached = cachedObjects[ctr];

                if (cached == Interlocked.CompareExchange<object>(ref cachedObjects[ctr], null, cached))
                    if (cached is IAware)
                        ((IAware)cached).DecrementCacheCount();
            }

            CheckNeedCleanCaches();
        }

        /// <summary>
        /// Whenever a GC has occured, the cached should be cleaned and all dead weak references removed
        /// </summary>
        private static void CheckNeedCleanCaches()
        {
            int priorGCCollectionCount = PriorGCCollectionCount;
            int myGCCount = GC.CollectionCount(GC.MaxGeneration);

            if (priorGCCollectionCount != myGCCount)
                if (priorGCCollectionCount == Interlocked.CompareExchange(ref PriorGCCollectionCount, myGCCount, priorGCCollectionCount))
                    ThreadPool.QueueUserWorkItem(delegate(object state)
                    {
                        LockFreeQueue<WeakReference> allCaches = AllCaches;
                        AllCaches = new LockFreeQueue<WeakReference>();

                        WeakReference weakReference;
                        while (allCaches.Dequeue(out weakReference))
                            try
                            {
                                {
                                    Cache cache = weakReference.Target as Cache;

                                    if (null != cache)
                                    {
                                        cache.RemoveCollectedCacheHandles();
                                        AllCaches.Enqueue(weakReference);
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                log.Warn("Exception while cleaning out old WeakReferences after a garbage collection", e);
                            }
                    });
        }

        private static long MemoryInUse = 0;

        /// <summary>
        /// The maximum amount of memory that large objects will use; ObjectCloud will release memory when large objects occupy more then the specified memory
        /// </summary>
        public static long MaximumMemoryToUse
        {
            get { return _MaximumMemoryToUse; }
            set
            {
                _MaximumMemoryToUse = value;
                ManageMemoryUse(0);
            }
        }
        private static long _MaximumMemoryToUse = long.MaxValue;

        /// <summary>
        /// Call to help ObjectCloud manually manage memory. For objects that hold large amounts of memory, they chould call this with the delta so ObjectCloud knows when to release memory. It is reccomended to call this prior to allocating and de-allocating memory so that there is free memory available
        /// </summary>
        /// <param name="delta"></param>
        public static void ManageMemoryUse(long delta)
        {
            long memoryInUse;

            do
            {
                memoryInUse = MemoryInUse;
            } while (memoryInUse != Interlocked.CompareExchange(ref MemoryInUse, memoryInUse + delta, memoryInUse));

            // Just write nulls into the cache until memory use falls within acceptable limits
            int tries = 0;
            while ((MemoryInUse > _MaximumMemoryToUse) && (tries < CachedObjects.Length))
            {
                CacheObject(null);
                tries++;
            }
        }
        
        /// <summary>
		/// Removes all of the collected cache handles from the cache 
		/// </summary>
		protected abstract void RemoveCollectedCacheHandles();

        /// <summary>
        /// Interface for objects that need to know if they are being handled in the cache
        /// </summary>
        public interface IAware
        {
            /// <summary>
            /// Called whenever the object is being stored in the cache
            /// </summary>
            void IncrementCacheCount();

            /// <summary>
            /// Called whenever the object is released from the cache
            /// </summary>
            void DecrementCacheCount();
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