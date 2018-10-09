using System;
using System.Collections.Generic;
using System.Threading;

namespace Test
{
    public sealed class Queue : IDisposable
    {
        private readonly LinkedList<Thread> _workers; 
        private readonly LinkedList<Action> _tasks = new LinkedList<Action>();
        private bool _disallowAdd;
        private bool _disposed; 

        public Queue()
        {
            var size = Environment.ProcessorCount-1;
            this._workers = new LinkedList<Thread>();
            for (var i = 0; i < size; ++i)
            {
                var worker = new Thread(this.Worker) { Name = string.Concat("Worker ", i)};
                worker.Start();
                this._workers.AddLast(worker);
            }
        }

        public void Dispose()
        {
            var waitForThreads = false;
            lock (this._tasks)
            {
                if (!this._disposed)
                {
                    GC.SuppressFinalize(this);

                    this._disallowAdd =
                        true; // wait for all tasks to finish processing while not allowing any more new tasks
                    while (this._tasks.Count > 0)
                    {
                        Monitor.Wait(this._tasks);
                    }

                    this._disposed = true;
                    Monitor.PulseAll(this
                        ._tasks); // wake all workers (none of them will be active at this point; disposed flag will cause then to finish so that we can join them)
                    waitForThreads = true;
                }
            }
            if (waitForThreads)
            {
                foreach (var worker in this._workers)
                {
                    worker.Join();
                }
            }
        }

        public void QueueTask(Action task)
        {
            lock (this._tasks)
            {
                if (this._disallowAdd)
                {
                    throw new InvalidOperationException(
                        "This Pool instance is in the process of being disposed, can't add anymore");
                }
                if (this._disposed)
                {
                    throw new ObjectDisposedException("This Pool instance has already been disposed");
                }
                this._tasks.AddLast(task);

                Monitor.PulseAll(this._tasks);
            }
        }


        private void Worker()
        {
            while (true) 
            {
                Action task = null;
                lock (this._tasks) 
                {
                    while (true) 
                    {
                        if (this._disposed)
                        {
                            return;
                        }
                        if (null != this._workers.First && 
                            ReferenceEquals(Thread.CurrentThread, this._workers.First.Value) &&
                            this._tasks.Count > 0
                        ) 
                        {
                            task = this._tasks.First.Value;
                            this._tasks.RemoveFirst();
                            this._workers.RemoveFirst();
                            Monitor.PulseAll(this._tasks); 
                            break; 
                        }
                        Monitor.Wait(this._tasks);
                    }
                }

                task(); 
                lock (this._tasks)
                {
                    this._workers.AddLast(Thread.CurrentThread);
                }
                task = null;
            }
        }
    }

}
