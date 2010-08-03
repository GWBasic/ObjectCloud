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
        private readonly Dictionary<TKey, CacheHandle> Dictionary =
            new Dictionary<TKey, CacheHandle>();

        /// <summary>
        /// Queue of key-value pairs to periodically check to see if they're still alive and thus valid
        /// </summary>
        private LockFreeQueue<KeyValuePair<TKey, CacheHandle>> CleanQueue = new LockFreeQueue<KeyValuePair<TKey, CacheHandle>>();

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
        /// Gets or creates a cache handle for the given key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private CacheHandle GetCacheHandle(TKey key)
        {
            Lock.EnterReadLock();
            try
            {
                // This little bit checks a cache handle to see if it's alive, and if it's not, deletes it
                // Basicaly, every time the cache is hit, some randome cache handle is checked to see if it's dead
                KeyValuePair<TKey, CacheHandle> toCheck;
                if (CleanQueue.Dequeue(out toCheck))
                {
                    if (!toCheck.Value.IsAlive)
                        new GenericArgument<TKey>(RemoveCollectedCacheHandle).BeginInvoke(toCheck.Key, null, null);
                }
                else
                    // If the clean queue is empty, then re-build it
                    CleanQueue.EnqueueAll(Dictionary);

                CacheHandle cacheHandle;
                if (Dictionary.TryGetValue(key, out cacheHandle))
                    return cacheHandle;
            }
            finally
            {
                Lock.ExitReadLock();
            }

            Lock.EnterWriteLock();
            try
            {
                // Get or create the cache handle
                CacheHandle cacheHandle;
                if (!Dictionary.TryGetValue(key, out cacheHandle))
                {
                    cacheHandle = new CacheHandle(this, key);
                    Dictionary[key] = cacheHandle;
                }

                return cacheHandle;
            }
            finally
            {
                Lock.ExitWriteLock();
            }
        }

        private void RemoveCollectedCacheHandle(TKey key)
        {
            try
            {
                CacheHandle cacheHandle;
                if (Dictionary.TryGetValue(key, out cacheHandle))
                {
                    // If another thread has the cache handle locked, then it's being ressurected and doesn't need to be deleted
                    if (Monitor.TryEnter(cacheHandle))
                        try
                        {
                            Lock.EnterWriteLock();

                            try
                            {
                                if (!cacheHandle.IsAlive)
                                    Dictionary.Remove(key);

                                cacheHandle.Valid = false;
                            }
                            finally
                            {
                                Lock.ExitWriteLock();
                            }
                        }
                        finally
                        {
                            Monitor.Exit(cacheHandle);
                        }
                }
            }
            catch (Exception e)
            {
                log.Warn("Error removing " + key.ToString() + " from the cache", e);
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
            TValue toReturn;
            CacheHandle cacheHandle;

            do
            {
                cacheHandle = GetCacheHandle(key);
                toReturn = cacheHandle.GetValue(constructorArg);

                /*try
                {
                    toReturn = cacheHandle.GetValue(constructorArg);
                }
                catch (OutOfMemoryException oome)
                {
                    ReleaseAllCachedMemory();

                    log.Warn("Out-of-memory when creating object for key " + key.ToString(), oome);

                    toReturn = cacheHandle.GetValue(constructorArg);
                }*/
            } while (!cacheHandle.Valid);

            return toReturn;
        }

        /// <summary>
        /// Explicit setter
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Set(TKey key, TValue value)
        {
            CacheHandle cacheHandle;

            do
            {
                cacheHandle = GetCacheHandle(key);
                cacheHandle.SetValue(value);
            }
            while (!cacheHandle.Valid);
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

            Lock.EnterWriteLock();
            try
            {
                if (Dictionary.TryGetValue(key, out cacheHandle))
                    Dictionary.Remove(key);
                else
                    return false;
            }
            finally
            {
                Lock.ExitWriteLock();
            }

            TValue cached = cacheHandle.WeakValue;
            cacheHandle.SetValue(null);

            if (null == cached)
                return false;

            if (cached is IDisposable)
                ((IDisposable)cached).Dispose();

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
                foreach (CacheHandle ch in Enumerable<CacheHandle>.FastCopy(Dictionary.Values))
                {
                    TValue value = ch.WeakValue;

                    if (null != value)
                        if (value is IDisposable)
                            ((IDisposable)value).Dispose();
                }

                Dictionary.Clear();
                CleanQueue = new LockFreeQueue<KeyValuePair<TKey, CacheHandle>>();
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
                IEnumerable<CacheHandle> toEnumerate;

                Lock.EnterReadLock();
                try
                {
                    toEnumerate = Enumerable<CacheHandle>.FastCopy(Dictionary.Values);
                }
                finally
                {
                    Lock.ExitReadLock();
                }

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
        private new class CacheHandle : Cache.CacheHandle
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
            /// This is set to false once the CacheHandle is invalid and shouldn't construct new objects
            /// </summary>
            internal bool Valid = true;

            /// <summary>
            /// Gets the value, constructing it if needed
            /// </summary>
            internal TValue GetValue(TConstructorArg constructorArg)
            {
                // If the value is still alive, increment counter without a lock, make sure the value stays alive, and return it
                TValue value = (TValue)WeakReference.Target;
                if (null != value)
                {
                    Value = value;

                    Cache.MoveToHeadOfCache(this);

                    return Value;
                }

                using (TimedLock.Lock(this))
                    if (Valid)
                    {
                        // A context switch could construct the value in another thread
                        value = (TValue)WeakReference.Target;

                        if (null == value)
                        {
                            // Try to force cleaning up memory if we run out
                            value = ParentCache._CreateForCache(Key, constructorArg);
                            WeakReference.Target = value;

                            Cache.AddToCache(this);
                        }
                        else
                            Cache.MoveToHeadOfCache(this);

                        Value = value;
                        return Value;
                    }
                    else
                        // The cache handle was invalidated and shouldn't construct a new object
                        return null;
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
                        Cache.AddToCache(this);
                }
            }

            internal override void AllowCollect()
            {
                Value = null;
            }

            internal bool IsAlive
            {
                get { return WeakReference.IsAlive; }
            }
        }
    }

    /// <summary>
    /// Provides basic functionality for shared cache management
    /// </summary>
    public abstract class Cache
    {
        internal static ILog log = LogManager.GetLogger<Cache>();

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
        /// Sub-thread for managing memory.  Allows the queue of memory to be managed without blocking the requesting threads, and in a synchronized manner
        /// </summary>
        protected static DelegateQueue DelegateQueue = new DelegateQueue("Cache manager");

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
        public static long MaxCacheReferences
        {
            get { return Cache._MaxCacheReferences; }
            set { Cache._MaxCacheReferences = value; }
        }
        private static long _MaxCacheReferences = long.MaxValue;

        /// <summary>
        /// The number of times that the cache is used prior to when RAM and garbage collections are inspected
        /// </summary>
        public static int CacheHitsPerInspection
        {
            get { return Cache._CacheHitsPerInspection; }
            set { Cache._CacheHitsPerInspection = value; }
        }
        private static int _CacheHitsPerInspection = 5000;

        /// <summary>
        /// Counter that is maintained to count cache hits.  When 0 == CacheHitsPerInspection mod CacheHitCount, then memory use is inspected
        /// </summary>
        private static int CacheHitCount = int.MinValue;

        /// <summary>
        /// The number of objects to dequeue every time there is a cache hit
        /// </summary>
        private static uint NumObjectsToDequeue = 0;

        /// <summary>
        /// The head of a doubly-linked queue of all cache handles
        /// </summary>
        private static CacheHandle Head = null;

        /// <summary>
        /// The tail of a double-linked queue of all cache handles
        /// </summary>
        private static CacheHandle Tail = null;

        private static long NumCacheReferences = 0;

        /// <summary>
        /// Moves the cache handle to the head of the cache queue
        /// </summary>
        /// <param name="cacheHandle"></param>
        protected static void MoveToHeadOfCache(CacheHandle cacheHandle)
        {
            DelegateQueue.QueueUserWorkItem(MoveToHeadOfCacheImpl, cacheHandle);
        }

        private static void MoveToHeadOfCacheImpl(object state)
        {
            CacheHandle cacheHandle = (CacheHandle)state;

            // The algorithm screws up when it's passed the head
            if (cacheHandle == Head)
                return;

            // If this handle isn't in the cache, increase the count
            if ((null == cacheHandle.Next) && (null == cacheHandle.Previous) && (Head != cacheHandle))
                NumCacheReferences++;

            else
            {
                // Remove from the cache

                if (Tail == cacheHandle)
                    Tail = cacheHandle.Previous;

                // Remove from the list
                if (null != cacheHandle.Previous)
                    cacheHandle.Previous.Next = cacheHandle.Next;

                if (null != cacheHandle.Next)
                    cacheHandle.Next.Previous = cacheHandle.Previous;
            }

            // Set up cache handle as head
            cacheHandle.Previous = null;
            cacheHandle.Next = Head;

            if (null != Head)
                Head.Previous = cacheHandle;

            Head = cacheHandle;

            if (null == Tail)
                Tail = cacheHandle;
        }

        /// <summary>
        /// Adds the cache handle to the cache, removing the least-recently-used cache handles from the cache
        /// </summary>
        /// <param name="cacheHandle"></param>
        protected static void AddToCache(CacheHandle cacheHandle)
        {
            // Queue a cache cleanup, if needed
            if ((Interlocked.Increment(ref CacheHitCount) % _CacheHitsPerInspection) == 0)
                DelegateQueue.QueueUserWorkItem(ManageCacheDeallocationRate);

            DelegateQueue.QueueUserWorkItem(MoveToHeadOfCacheImpl, cacheHandle);
            DelegateQueue.QueueUserWorkItem(DequeueImpl);
        }

        /// <summary>
        /// Dequeues from the cache
        /// </summary>
        /// <param name="state"></param>
        private static void DequeueImpl(object state)
        {
            // If there are too many objects in the cache, then extra need to be dequeued
            long numObjectsToDequeue = NumCacheReferences - MaxCacheReferences;
            if (numObjectsToDequeue > 0)
                numObjectsToDequeue += NumObjectsToDequeue;
            else
                numObjectsToDequeue = NumObjectsToDequeue;

            for (int ctr = 0; ctr < numObjectsToDequeue; ctr++)
                if (NumCacheReferences > MinCacheReferences)
                    if (null != Tail)
                    {
                        CacheHandle tail = Tail;

                        Tail = tail.Previous;

                        if (null != Tail)
                            Tail.Next = null;
                        else
                            Head = null;

                        tail.AllowCollect();

                        // In case the handle is re-used, setting these both to null ensures that it will be counted as a new handle
                        tail.Next = null;
                        tail.Previous = null;

                        NumCacheReferences--;
                    }
        }

        /// <summary>
        /// The current process
        /// </summary>
        static private Process MyProcess = Process.GetCurrentProcess();

        /// <summary>
        /// Manages deallocation of memory
        /// </summary>
        internal static void ManageCacheDeallocationRate(object state)
        {
            // Only let one thread perform cleanup at a time, and don't let blocked iterations sit around
            try
            {
                long maxMemory;

                if (null == MaxMemory)
                {
                    double maxWorkingSet = PercentOfMaxWorkingSet * (Convert.ToDouble(MyProcess.MaxWorkingSet.ToInt64()) / 0.001024);
                    maxMemory = Convert.ToInt64(maxWorkingSet);
                }
                else
                    maxMemory = MaxMemory.Value;

                long processMemorySize = GC.GetTotalMemory(false);

                // If too much memory is used, force a GC to get a better estimate
                if (processMemorySize >= maxMemory)
                    processMemorySize = GC.GetTotalMemory(true);

                if (processMemorySize >= maxMemory)
                {
                    log.WarnFormat("Dumping Cache:\tProcess Memory Estimate: {0}\n\tMax Memory: {1}\n\tMax Working Set: {2}",
                        processMemorySize, maxMemory, MyProcess.MaxWorkingSet);

                    NumObjectsToDequeue = Convert.ToUInt32(NumCacheReferences / 10);

                    do
                    {
                        DequeueImpl(null);
                        processMemorySize = GC.GetTotalMemory(true);
                    }
                    while ((NumCacheReferences > MinCacheReferences) && (processMemorySize > maxMemory));
                }

                IEnumerable<long> memorySizeLimits;

                if (null != MemorySizeLimits)
                    memorySizeLimits = MemorySizeLimits;
                else
                    memorySizeLimits = new long[]
                    {
                        (maxMemory * 60) / 100,
                        (maxMemory * 80) / 100,
                        (maxMemory * 85) / 100,
                        (maxMemory * 90) / 100,
                        (maxMemory * 95) / 100
                    };

                uint numObjectsToDequeue = 0;
                foreach (long memoryLevel in memorySizeLimits)
                    if (processMemorySize >= memoryLevel)
                        numObjectsToDequeue++;

                NumObjectsToDequeue = numObjectsToDequeue;
            }
            catch (Exception e)
            {
                log.Error("Error in the RAM cache!!!", e);
            }
        }

        /// <summary>
        /// Clears all in-memory cached objects, except those which have strong references elsewhere
        /// </summary>
        public static void ReleaseAllCachedMemory()
        {
            object signal = new object();

            lock (signal)
            {
                DelegateQueue.Cancel();
                DelegateQueue.QueueUserWorkItem(ReleaseAllCachedMemoryImpl, signal);

                Monitor.Wait(signal, 3000);
            }
        }

        private static void ReleaseAllCachedMemoryImpl(object signal)
        {
            while (null != Head)
            {
                Head.AllowCollect();
                Head = Head.Next;
            }

            Tail = null;

            GC.Collect(GC.MaxGeneration);

            ManageCacheDeallocationRate(null);

            lock (signal)
                Monitor.Pulse(signal);
        }

        /// <summary>
        /// Represents a doubly-linked list of items that are cached
        /// </summary>
        protected abstract class CacheHandle
        {
            internal CacheHandle Next = null;
            internal CacheHandle Previous = null;

            /// <summary>
            /// Indicates that the cache handle will only keep a weak reference to the object, thus allowing it be collected
            /// </summary>
            internal abstract void AllowCollect();
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