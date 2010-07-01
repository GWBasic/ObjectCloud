// See http://www.boyet.com/Articles/LockFreeRedux.html

/*
Copyright (c) 2010 Julian M Bucknall

Permission is hereby granted, free of charge, to any person obtaining a copy of this software
and associated documentation files (the "Software"), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute,
sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN
AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

// Note:  Contains modifications by Andrew Rondeau for ObjectCloud

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ObjectCloud.Common.Threading
{
    public class LockFreeQueue<T>
    {
        SingleLinkNode<T> Head;
        SingleLinkNode<T> Tail;

        public LockFreeQueue()
        {
            Head = new SingleLinkNode<T>();
            Tail = Head;
        }

        public virtual void Enqueue(T item)
        {
            SingleLinkNode<T> oldTail = null;
            SingleLinkNode<T> oldTailNext;

            SingleLinkNode<T> newNode = new SingleLinkNode<T>();
            newNode.Item = item;

            bool newNodeWasAdded = false;
            while (!newNodeWasAdded)
            {
                oldTail = Tail;
                oldTailNext = oldTail.Next;

                if (Tail == oldTail)
                {
                    if (oldTailNext == null)
                        newNodeWasAdded = SyncMethods.CAS<SingleLinkNode<T>>(ref Tail.Next, null, newNode);
                    else
                        SyncMethods.CAS<SingleLinkNode<T>>(ref Tail, oldTail, oldTailNext);
                }
            }

            SyncMethods.CAS<SingleLinkNode<T>>(ref Tail, oldTail, newNode);
        }

        public virtual bool Dequeue(out T item)
        {
            item = default(T);
            SingleLinkNode<T> oldHead = null;

            bool haveAdvancedHead = false;
            while (!haveAdvancedHead)
            {

                oldHead = Head;
                SingleLinkNode<T> oldTail = Tail;
                SingleLinkNode<T> oldHeadNext = oldHead.Next;

                if (oldHead == Head)
                {
                    if (oldHead == oldTail)
                    {
                        if (oldHeadNext == null)
                        {
                            return false;
                        }
                        SyncMethods.CAS<SingleLinkNode<T>>(ref Tail, oldTail, oldHeadNext);
                    }

                    else
                    {
                        item = oldHeadNext.Item;
                        haveAdvancedHead =
                          SyncMethods.CAS<SingleLinkNode<T>>(ref Head, oldHead, oldHeadNext);
                    }
                }
            }
            return true;
        }

        public T Dequeue()
        {
            T result;
            Dequeue(out result);
            return result;
        }
    }


    /// <summary>
    /// Adds a count to the LockFreeQueue
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class LockFreeQueue_WithCount<T> : LockFreeQueue<T>
    {
        /// <summary>
        /// The number of items in the stack.  This might be inaccurate if a concurrent thread is pushing or popping, thus it should be treated as an estimate
        /// </summary>
        public long Count
        {
            get { return _Count; }
        }
        private long _Count = 0;

        public override void Enqueue(T item)
        {
            base.Enqueue(item);
            Interlocked.Increment(ref _Count);
        }

        public override bool Dequeue(out T item)
        {
            bool dequeued = base.Dequeue(out item);

            if (dequeued)
                Interlocked.Decrement(ref _Count);

            return dequeued;
        }
    }
}