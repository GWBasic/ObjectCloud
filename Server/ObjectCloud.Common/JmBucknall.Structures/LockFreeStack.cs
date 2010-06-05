// See http://www.boyet.com/Articles/LockFreeRedux.html

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using JmBucknall.Threading;

namespace JmBucknall.Structures
{
    public class LockFreeStack<T>
    {
        private SingleLinkNode<T> head;

        public LockFreeStack()
        {
            head = new SingleLinkNode<T>();
        }

        public virtual void Push(T item)
        {
            SingleLinkNode<T> newNode = new SingleLinkNode<T>();
            newNode.Item = item;
            do
            {
                newNode.Next = head.Next;
            } while (!SyncMethods.CAS<SingleLinkNode<T>>(ref head.Next, newNode.Next, newNode));
        }

        public virtual bool Pop(out T item)
        {
            SingleLinkNode<T> node;
            do
            {
                node = head.Next;
                if (node == null)
                {
                    item = default(T);
                    return false;
                }
            } while (!SyncMethods.CAS<SingleLinkNode<T>>(ref head.Next, node, node.Next));
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