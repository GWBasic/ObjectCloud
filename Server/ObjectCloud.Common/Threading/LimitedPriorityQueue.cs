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


using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace JmBucknall.Structures {

  public class LimitedPriorityQueue<T,P> {

    private IPriorityConverter<P> converter;
    private LockFreeQueue<T>[] queueList;

    public LimitedPriorityQueue(IPriorityConverter<P> converter) {
      this.converter = converter;
      this.queueList = new LockFreeQueue<T>[converter.PriorityCount];
      for (int i = 0; i < queueList.Length; i++) {
        queueList[i] = new LockFreeQueue<T>();
      }
    }

    public void Enqueue(T item, P priority) {
      this.queueList[converter.Convert(priority)].Enqueue(item);
    }

    public bool Dequeue(out T item) {
      foreach (LockFreeQueue<T> q in queueList) {
        if (q.Dequeue(out item)) {
          return true;
        }
      }
      item = default(T);
      return false;
    }

    public T Dequeue() {
      T result;
      Dequeue(out result);
      return result;
    }
  }
}
