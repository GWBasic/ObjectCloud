// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using ObjectCloud.Common;

namespace TimeLocks
{
    class Tester
    {
        public Tester() { }

        public Tester(ISyncronized syncronized)
        {
            _Syncronized = syncronized;
        }

        /// <summary>
        /// The syncronized object to test
        /// </summary>
        internal ISyncronized Syncronized
        {
            get { return _Syncronized; }
            set { _Syncronized = value; }
        }
        private ISyncronized _Syncronized;

        /// <summary>
        /// The number of times to iterate
        /// </summary>
        private const int NumIterations = 1000000;
		
		/// <summary>
		/// When mixing reads and writes, the chance that a write will occur 
		/// </summary>
		private const int WriteChance = 1000;

        public TimeSpan TestReadOnSingleThread()
        {
			GC.Collect(int.MaxValue, GCCollectionMode.Forced);
			
            DateTime start = DateTime.UtcNow;

            int dummy;
            for (int ctr = 0; ctr < NumIterations; ctr++)
                dummy = Syncronized.Prop;

            return DateTime.UtcNow - start;
        }

        public TimeSpan TestWriteOnSingleThread()
        {
			GC.Collect(int.MaxValue, GCCollectionMode.Forced);
			
            DateTime start = DateTime.UtcNow;

            int dummy;
            for (int ctr = 0; ctr < NumIterations; ctr++)
				if (0 == SRandom.Next(WriteChance))
					Syncronized.Prop = 123;
				else
                		dummy = Syncronized.Prop;

            return DateTime.UtcNow - start;
        }

        public TimeSpan TestReadOnMultipleThreads(int numThreads)
        {
            ThreadStart threadStart = delegate()
            {
                int dummy;
                for (int ctr = 0; ctr < NumIterations; ctr++)
                    dummy = Syncronized.Prop;
            };

            LinkedList<Thread> threads = new LinkedList<Thread>();

            for (int ctr = 0; ctr < numThreads; ctr++)
                threads.AddLast(new Thread(threadStart));

			GC.Collect(int.MaxValue, GCCollectionMode.Forced);
			
            DateTime start = DateTime.UtcNow;

            foreach (Thread thread in threads)
                thread.Start();

            foreach (Thread thread in threads)
                thread.Join();

            return DateTime.UtcNow - start;
        }

        public TimeSpan TestWriteOnMultipleThreads(int numThreads)
        {
            ThreadStart threadStart = delegate()
            {
		        int dummy;
		        for (int ctr = 0; ctr < NumIterations; ctr++)
					if (0 == SRandom.Next(WriteChance))
						Syncronized.Prop = 123;
					else
		            		dummy = Syncronized.Prop;
            };

            LinkedList<Thread> threads = new LinkedList<Thread>();

            for (int ctr = 0; ctr < numThreads; ctr++)
                threads.AddLast(new Thread(threadStart));

			GC.Collect(int.MaxValue, GCCollectionMode.Forced);
			
            DateTime start = DateTime.UtcNow;

            foreach (Thread thread in threads)
                thread.Start();

            foreach (Thread thread in threads)
                thread.Join();

            return DateTime.UtcNow - start;
        }
    }
}
