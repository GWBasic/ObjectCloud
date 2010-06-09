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

        SingleLinkNode<T> head;
        SingleLinkNode<T> tail;

        public LockFreeQueue()
        {
            head = new SingleLinkNode<T>();
            tail = head;
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
                oldTail = tail;
                oldTailNext = oldTail.Next;

                if (tail == oldTail)
                {
                    if (oldTailNext == null)
                        newNodeWasAdded = SyncMethods.CAS<SingleLinkNode<T>>(ref tail.Next, null, newNode);
                    else
                        SyncMethods.CAS<SingleLinkNode<T>>(ref tail, oldTail, oldTailNext);
                }
            }

            SyncMethods.CAS<SingleLinkNode<T>>(ref tail, oldTail, newNode);
        }

        public virtual bool Dequeue(out T item)
        {
            item = default(T);
            SingleLinkNode<T> oldHead = null;

            bool haveAdvancedHead = false;
            while (!haveAdvancedHead)
            {

                oldHead = head;
                SingleLinkNode<T> oldTail = tail;
                SingleLinkNode<T> oldHeadNext = oldHead.Next;

                if (oldHead == head)
                {
                    if (oldHead == oldTail)
                    {
                        if (oldHeadNext == null)
                        {
                            return false;
                        }
                        SyncMethods.CAS<SingleLinkNode<T>>(ref tail, oldTail, oldHeadNext);
                    }

                    else
                    {
                        item = oldHeadNext.Item;
                        haveAdvancedHead =
                          SyncMethods.CAS<SingleLinkNode<T>>(ref head, oldHead, oldHeadNext);
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

            long count;
            long oldCount;
            do
            {
                oldCount = _Count;
                count = oldCount + 1;
            } while (oldCount != Interlocked.CompareExchange(ref _Count, count, oldCount));
        }

        public override bool Dequeue(out T item)
        {
            bool dequeued = base.Dequeue(out item);

            if (dequeued)
            {
                long count;
                long oldCount;
                do
                {
                    oldCount = _Count;
                    count = oldCount - 1;
                } while (oldCount != Interlocked.CompareExchange(ref _Count, count, oldCount));
            }

            return dequeued;
        }
    }
}