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
    public class LockFreeStack<T>
    {
        private SingleLinkNode<T> Head;

        public LockFreeStack()
        {
            Head = new SingleLinkNode<T>();
        }

        public virtual void Push(T item)
        {
            SingleLinkNode<T> newNode = new SingleLinkNode<T>();
            newNode.Item = item;
            do
            {
                newNode.Next = Head.Next;
            } while (!SyncMethods.CAS<SingleLinkNode<T>>(ref Head.Next, newNode.Next, newNode));
        }

        public virtual bool Pop(out T item)
        {
            SingleLinkNode<T> node;
            do
            {
                node = Head.Next;
                if (node == null)
                {
                    item = default(T);
                    return false;
                }
            } while (!SyncMethods.CAS<SingleLinkNode<T>>(ref Head.Next, node, node.Next));
            item = node.Item;
            return true;
        }

        public T Pop()
        {
            T result;
            Pop(out result);
            return result;
        }
    }

    /// <summary>
    /// Adds a count to the LockFreeStack
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class LockFreeStack_WithCount<T> : LockFreeStack<T>
    {
        /// <summary>
        /// The number of items in the stack.  This might be inaccurate if a concurrent thread is pushing or popping, thus it should be treated as an estimate
        /// </summary>
        public long Count
        {
            get { return _Count; }
        }
        private long _Count = 0;

        public override void Push(T item)
        {
            base.Push(item);

            long count;
            long oldCount;
            do
            {
                oldCount = _Count;
                count = oldCount + 1;
            } while (oldCount != Interlocked.CompareExchange(ref _Count, count, oldCount));
        }

        public override bool Pop(out T item)
        {
            bool popped = base.Pop(out item);

            if (popped)
            {
                long count;
                long oldCount;
                do
                {
                    oldCount = _Count;
                    count = oldCount - 1;
                } while (oldCount != Interlocked.CompareExchange(ref _Count, count, oldCount));
            }

            return popped;
        }
    }
}
