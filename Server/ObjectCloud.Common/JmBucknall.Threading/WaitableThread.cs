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
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace JmBucknall.Threading {
  public class WaitableThread : WaitHandle {
    private ManualResetEvent signal;
    private ThreadStart startThread;
    private Thread thread;

    public WaitableThread(ThreadStart start) {
      this.startThread = start;
      this.signal = new ManualResetEvent(false);
      this.SafeWaitHandle = signal.SafeWaitHandle;
      this.thread = new Thread(new ThreadStart(ExecuteDelegate));
    }

    protected override void Dispose(bool disposing) {
      if (disposing) {
        signal.Close();
        this.SafeWaitHandle = null;
      }
      base.Dispose(disposing);
    }

    public void Abort() {
      thread.Abort();
    }

    public void Join() {
      thread.Join();
    }

    public void Start() {
      thread.Start();
    }

    private void ExecuteDelegate() {
      signal.Reset();
      try {
        startThread();
      }
      finally {
        signal.Set();
      }
    }

  }
}
