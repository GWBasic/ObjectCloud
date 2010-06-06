using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

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
        private int NumIterations = 1000000;

        public TimeSpan TestReadOnSingleThread()
        {
            DateTime start = DateTime.UtcNow;

            int dummy;
            for (int ctr = 0; ctr < NumIterations; ctr++)
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

            DateTime start = DateTime.UtcNow;

            foreach (Thread thread in threads)
                thread.Start();

            foreach (Thread thread in threads)
                thread.Join();

            return DateTime.UtcNow - start;
        }
    }
}
