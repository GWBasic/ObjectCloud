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

        private ReaderWriterLockSlim Lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        protected readonly CreateForCache<TKey, TValue, TConstructorArg> _CreateForCache;

        public Cache(CreateForCache<TKey, TValue, TConstructorArg> createForCache)
        {
            _CreateForCache = createForCache;
			AllCaches.Enqueue(new WeakReference(this));
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
                    foreach (KeyValuePair<TKey, CacheHandle> kvp in Dictionary)
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
                        CacheHandle cacheHandle;
                        foreach (TKey key in toRemove)
                            if (Dictionary.TryGetValue(key, out cacheHandle))
                            {
                                // If another thread has the cache handle locked, then it's being ressurected and doesn't need to be deleted
                                if (Monitor.TryEnter(cacheHandle))
                                    try
                                    {
                                        if (!cacheHandle.IsAlive)
                                        {
                                            Dictionary.Remove(key);
                                            cacheHandle.Valid = false;
                                        }
                                    }
                                    finally
                                    {
                                        Monitor.Exit(cacheHandle);
                                    }
                            }
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
            TValue toReturn;
            CacheHandle cacheHandle;

            do
            {
                cacheHandle = GetCacheHandle(key);
                toReturn = cacheHandle.GetValue(constructorArg);
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

            // Removes the cache handle from the queue of cached objects, this ensures that memory is free for other objects
            Remove(cacheHandle);

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
                foreach (CacheHandle cacheHandle in Enumerable<CacheHandle>.FastCopy(Dictionary.Values))
                {
                    TValue value = cacheHandle.WeakValue;

                    if (null != value)
                        if (value is IDisposable)
                            ((IDisposable)value).Dispose();

                    Remove(cacheHandle);
                }

                Dictionary.Clear();
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
        /// If the cache is GCed, then it'll attempt to remove its cache handles from the cache
        /// </summary>
        ~Cache()
        {
            try
            {
                foreach (CacheHandle cacheHandle in Dictionary.Values)
                    Remove(cacheHandle);
            }
            catch { }
        }

        /// <summary>
        /// Handle to an object that manages constructing it if needed
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        private new class CacheHandle : Cache.CacheHandle
        {
            /// <summary>
            /// The parent cache.  This is a weak reference so that, if the only reference to this object is in the cache, it is collected
            /// </summary>
            internal WeakReference ParentCache;

            /// <summary>
            /// The key
            /// </summary>
            internal TKey Key;

            internal CacheHandle(Cache<TKey, TValue, TConstructorArg> parentCache, TKey key)
            {
                ParentCache = new WeakReference(parentCache);
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
                    return value;
                }

                using (TimedLock.Lock(this))
                    if (Valid)
                    {
                        // A context switch could construct the value in another thread
                        value = (TValue)WeakReference.Target;

                        if (null == value)
                        {
                            // Try to force cleaning up memory if we run out
                            value = ((Cache<TKey, TValue, TConstructorArg>)ParentCache.Target)._CreateForCache(Key, constructorArg);
                            WeakReference.Target = value;

                            Cache.AddToCache(this);

                            if (Value is Cache.IAware)
                                ((Cache.IAware)Value).IncrementCacheCount();
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
                    // If there is an existing value, allow it to be collected
                    if (null != Value)
                        AllowCollect();

                    Value = value;
                    WeakReference.Target = value;

                    if (null != value)
                        Cache.AddToCache(this);

                    if (Value is Cache.IAware)
                        ((Cache.IAware)Value).IncrementCacheCount();
                }
            }

            internal override void AllowCollect()
            {
                if (Value is Cache.IAware)
                    ((Cache.IAware)Value).DecrementCacheCount();

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

        private static long NumCaches = 0;

        internal Cache() 
        {
            if (1 == Interlocked.Increment(ref NumCaches))
                DelegateQueue = new DelegateQueue("Cache manager");
        }

        ~Cache()
        {
            if (0 == Interlocked.Decrement(ref NumCaches))
            {
                DelegateQueue.Dispose();
                DelegateQueue = null;
            }
        }

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
        protected static DelegateQueue DelegateQueue;

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
        private static int _CacheHitsPerInspection = 500;

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
            if (!cacheHandle.InQueue)
            {
                NumCacheReferences++;
                cacheHandle.InQueue = true;
            }

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
            DelegateQueue.QueueUserWorkItem(MoveToHeadOfCacheImpl, cacheHandle);
            DelegateQueue.QueueUserWorkItem(DequeueImpl);
        }
		
		/// <summary>
		/// Used to monitor the garbage collector.  Whenever a collection occurs, memory is managed 
		/// </summary>
		private static int LastCollectionCount = 0;

        /// <summary>
        /// Dequeues from the cache
        /// </summary>
        /// <param name="state"></param>
        private static void DequeueImpl(object state)
        {
			// Only manage the deallocation rate as the garbage is collected
            int generation = GC.MaxGeneration - 1;
            if (generation < 0)
                generation = 0;

			int currentCollectionCount = GC.CollectionCount(generation);
			if (currentCollectionCount != LastCollectionCount)
			{
                // For some unknown reason, this isn't enough to prevent a stack overflow!
                LastCollectionCount = currentCollectionCount;
				ManageCacheDeallocationRate(null);
			}
			
            // If there are too many objects in the cache, then extra need to be dequeued
            long numObjectsToDequeue = NumCacheReferences - MaxCacheReferences;
            if (numObjectsToDequeue > 0)
                numObjectsToDequeue += NumObjectsToDequeue;
            else
                numObjectsToDequeue = NumObjectsToDequeue;

            for (int ctr = 0; ctr < numObjectsToDequeue; ctr++)
                if (NumCacheReferences > MinCacheReferences)
                    if (null != Tail)
                        RemoveImpl(Tail);
        }

        /// <summary>
        /// Removes the cache handle from the queue
        /// </summary>
        /// <param name="toRemove"></param>
        protected static void Remove(CacheHandle toRemove)
        {
            DelegateQueue.QueueUserWorkItem(RemoveImpl, toRemove);
        }

        private static void RemoveImpl(object state)
        {
            CacheHandle toRemove = (CacheHandle)state;

            // Removing a cache handle that's not in the queue can screw things up
            if (!toRemove.InQueue)
                return;

            NumCacheReferences--;

            toRemove.InQueue = false;
            toRemove.AllowCollect();

            // If removing the tail, then move it back one
            if (Tail == toRemove)
                Tail = toRemove.Previous;

            // If the tail was set to null, then the head also needs to be null
            if (null == Tail)
            {
                Head = null;
                return;
            }

            if (null != toRemove.Previous)
                toRemove.Previous.Next = toRemove.Next;

            if (null != toRemove.Next)
                toRemove.Next.Previous = toRemove.Previous;

            // Make sure that references to other handles aren't kept as this can impede garbage collection
            toRemove.Next = null;
            toRemove.Previous = null;
        }

        /// <summary>
        /// The current process
        /// </summary>
        static private Process MyProcess = Process.GetCurrentProcess();

        /// <summary>
        /// Prevents recursion in ManageCacheDeallocationRate
        /// </summary>
        static bool ManageCacheDeallocationRate_RecusionBlocker = true;

        /// <summary>
        /// Manages deallocation of memory
        /// </summary>
        internal static void ManageCacheDeallocationRate(object state)
        {
            if (ManageCacheDeallocationRate_RecusionBlocker)
                try
                {
                    ManageCacheDeallocationRate_RecusionBlocker = false;

                    // Calls RemoveCollectedCacheHandles on all Caches 

                    LockFreeQueue<WeakReference> allCaches = AllCaches;
                    AllCaches = new LockFreeQueue<WeakReference>();

                    WeakReference wr;
                    while (allCaches.Dequeue(out wr))
                    {
                        Cache cache = (Cache)wr.Target;

                        if (null != cache)
                        {
                            // When the server is busy, clean out collected cache handles on multiple threads
                            // This will end the "busy" state sooner so the server can continue accepting requests
                            if (Busy.IsBusy)
                            {
                                GenericVoid removeCollectedCacheHandlesDelegate = cache.RemoveCollectedCacheHandles;
                                removeCollectedCacheHandlesDelegate.BeginInvoke(null, null);
                            }
                            else
                                // When the server isn't busy, do all cleaning on a single thread so more CPU power
                                // is available for requests
                                cache.RemoveCollectedCacheHandles();

                            AllCaches.Enqueue(wr);
                        }
                    }

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

                        if (NumObjectsToDequeue < 30)
                            NumObjectsToDequeue = 30;

                        int iterations = 0;

                        do
                        {
                            DequeueImpl(null);
                            processMemorySize = GC.GetTotalMemory(true);

                            iterations++;
                        }
                        while ((NumCacheReferences > MinCacheReferences) && (processMemorySize > maxMemory) && (iterations < 10));

                        //throw new NotImplementedException("Raise event when this error condition occurs in case there's a way to reset the process");
                        // For now, if too many iterations occur, try to kill the entire cache
                        if (iterations >= 10)
                        {
                            log.Warn("Possible memory leak, killing cache");

                            DelegateQueue.Cancel();
                            ReleaseAllCachedMemoryImpl(null);

                            // Releasing all memory re-queues this function
                            return;
                        }

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
                finally
                {
                    ManageCacheDeallocationRate_RecusionBlocker = true;
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
            NumCacheReferences = 0;

            GC.Collect(GC.MaxGeneration);

            ManageCacheDeallocationRate(null);

            lock (signal)
                Monitor.Pulse(signal);
        }
		
		/// <summary>
		/// Queues a garbage collection on the cache's memory management thread, and then manages the cache afterwards 
		/// </summary>
		public static void QueueGC()
		{
			DelegateQueue.QueueUserWorkItem(DoGC);
		}
		
		private static void DoGC(object state)
		{
			DateTime start = DateTime.UtcNow;
			
			GC.Collect(GC.MaxGeneration);
			
			TimeSpan collectionTime = DateTime.UtcNow - start;
			
			string logString = "Idle garbage collection took " + collectionTime.TotalSeconds.ToString() + " seconds";
			
			if (collectionTime.TotalMilliseconds <= 250)
				log.Info(logString);
			else
				log.Warn(logString);
			
			ManageCacheDeallocationRate(state);
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

            /// <summary>
            /// True when the cache handle is in the queue, false when it's out of the queue
            /// </summary>
            internal bool InQueue = false;
        }
		
		/// <summary>
		/// Weak references to all in-memory caches
		/// </summary>
		protected static LockFreeQueue<WeakReference> AllCaches = new LockFreeQueue<WeakReference>();
		
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