using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace CustomThreadPool
{
    public sealed class CustomThreadPool : IDisposable
    {

        private readonly Queue _workItemsQueue = Queue.Synchronized(new Queue());
        private readonly List<WorkerThread> _workerThreads = new List<WorkerThread>();
        private static readonly int NumberOfThreads = Environment.ProcessorCount;
        private readonly AutoResetEvent _newWorkItemsAvailableWait = new AutoResetEvent(false);
        private volatile bool _disposed;

        public CustomThreadPool()
        {
            for (int i = 0; i < NumberOfThreads; i++)
            {
                _workerThreads.Add(new WorkerThread());
            }
            new Thread(MainExecutionThread).Start();
        }

        public CustomThreadPool(int numberOfThreads)
        {
            for (int i = 0; i < numberOfThreads; i++)
            {
                _workerThreads.Add(new WorkerThread());
            }
            new Thread(MainExecutionThread).Start();
        }

        public void QueueUserWorkItem(Action workItem)
        {
            _workItemsQueue.Enqueue(new WorkQueueItem(workItem));
            _newWorkItemsAvailableWait.Set();
        }

        public IAsyncResult QueueUserWorkItemResult<TResult>(Func<TResult> workItem, AsyncCallback asyncCallback)
        {
            var asyncResult = new CustomAsyncResult(asyncCallback);
            _workItemsQueue.Enqueue(new WorkQueueItem(workItem, asyncResult));
            _newWorkItemsAvailableWait.Set();
            return asyncResult;
        }

        public static TResult RetreiveUserWorkItemResult<TResult>(IAsyncResult asyncResult)
        {
            var custAsyncResult = asyncResult as CustomAsyncResult;
            if (custAsyncResult == null)
            {
                throw new InvalidOperationException("Unexpected IAsyncResult instance passed");
            }
            (custAsyncResult as IAsyncResult).AsyncWaitHandle.WaitOne();
            if (custAsyncResult.ExceptionOccured)
            {
                throw custAsyncResult.Exception;
            }
            return (TResult)custAsyncResult.Result;
        }


        private void MainExecutionThread()
        {
            while (true)
            {
                
                
                while (_workItemsQueue.Count > 0)
                {
                    if(_disposed) return;

                    var workItem = _workItemsQueue.Dequeue() as WorkQueueItem;
                    if (!_workerThreads.Any(x => x.IsAvailable))
                    {
                        var newThread = new WorkerThread();
                        _workerThreads.Add(newThread);
                    }
                    WorkerThread availableThread = _workerThreads.First(x => x.IsAvailable);
                    availableThread.ExecuteWorkItem(workItem);
                }

                if (_disposed) return;
                _newWorkItemsAvailableWait.WaitOne();
                if (_disposed) return;
            }
        }

        #region IDisposable Members

        void IDisposable.Dispose()
        {
            GC.SuppressFinalize(this);
            DisposeImpl();
        }

        ~CustomThreadPool()
        {
            DisposeImpl();
        }

        private void DisposeImpl()
        {
            _disposed = true;
            if (_newWorkItemsAvailableWait != null)
            {
                _newWorkItemsAvailableWait.Set(); //let background thread proceed and exit
                (_newWorkItemsAvailableWait as IDisposable).Dispose();
            }
            if (_workerThreads != null)
            {
                foreach (IDisposable workerThread in _workerThreads)
                {
                    workerThread.Dispose();
                }
            }
        }
        #endregion

        private class WorkQueueItem
        {
            public WorkQueueItem(MulticastDelegate del)
            {
                Delegate = del;
                ExecutionMode = WorkThreadExecutionMode.Simple;
            }

            public WorkQueueItem(MulticastDelegate del, CustomAsyncResult asyncResult)
            {
                Delegate = del;
                ExecutionMode = WorkThreadExecutionMode.AsyncResult;
                AsyncResult = asyncResult;
            }

            public MulticastDelegate Delegate { get; private set; }
            public CustomAsyncResult AsyncResult { get; private set; }
            public WorkThreadExecutionMode ExecutionMode { get; private set; }
        }

        private class WorkerThread : IDisposable
        {
            private readonly Thread _thread;
            private readonly AutoResetEvent _waitForNewTask = new AutoResetEvent(false);
            private volatile bool _isAvailable = true;
            private WorkThreadExecutionMode _executionMode;
            private MulticastDelegate _task;
            private CustomAsyncResult _asyncResult;
            private volatile bool _disposed;


            public WorkerThread()
            {
                _thread = new Thread(MainWorkerThread);
                _thread.Start();
            }

            public bool IsAvailable
            {
                get { return _isAvailable; }
            }

            private void MainWorkerThread()
            {
                while (true)
                {
                    if (_disposed) return;
                    _waitForNewTask.WaitOne();
                    if (_disposed) return;

                    try
                    {
                        object result = _task.DynamicInvoke(null);
                        if (_executionMode == WorkThreadExecutionMode.AsyncResult)
                        {
                            _asyncResult.SetCompleted(result);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_executionMode == WorkThreadExecutionMode.AsyncResult)
                        {
                            _asyncResult.SetCompletedException(ex);
                        }
                    }
                    finally
                    {
                        _isAvailable = true;
                        _asyncResult = null;
                        _task = null;
                    }
                }
            }

            public void ExecuteWorkItem(WorkQueueItem workItem)
            {
                if (!_isAvailable)
                {
                    throw new InvalidOperationException("Thread is busy");
                }

                _executionMode = workItem.ExecutionMode;
                _task = workItem.Delegate;
                _asyncResult = workItem.AsyncResult;
                _isAvailable = false;
                _waitForNewTask.Set();
            }

            #region IDisposable Members

            void IDisposable.Dispose()
            {
                GC.SuppressFinalize(this);
                DisposeImpl();
            }

            ~WorkerThread()
            {
                DisposeImpl();
            }

            private void DisposeImpl()
            {
                _disposed = true;
                if (_waitForNewTask != null)
                {
                    _waitForNewTask.Set();
                    (_waitForNewTask as IDisposable).Dispose();
                }

            }
            #endregion
        }

        private enum WorkThreadExecutionMode
        {
            AsyncResult,
            Simple
        }

        private class CustomAsyncResult : IAsyncResult
        {
            private bool _isCompleted;
            private ManualResetEvent _waitHandle;
            private object _result;
            private AsyncCallback _asyncCallback;
            private Exception _ex;
            private readonly object _syncObject = new object();

            public CustomAsyncResult(AsyncCallback asyncCallback)
            {
                _waitHandle = new ManualResetEvent(false);
                _asyncCallback = asyncCallback;
            }

            #region IAsyncResult Members
            object IAsyncResult.AsyncState
            {
                get { throw new NotImplementedException("State is not supported"); }
            }

            WaitHandle IAsyncResult.AsyncWaitHandle
            {
                get { return _waitHandle; }
            }

            bool IAsyncResult.CompletedSynchronously
            {
                get { return false; }
            }

            bool IAsyncResult.IsCompleted
            {
                get { lock (_syncObject) { return _isCompleted; } }
            }

            #endregion

            public object Result
            {
                get
                {
                    lock (_syncObject)
                    {
                        return _result;
                    }
                }
            }

            public bool ExceptionOccured
            {
                get
                {
                    lock (_syncObject)
                    {
                        return _ex != null;
                    }
                }
            }

            public Exception Exception
            {
                get
                {
                    lock (_syncObject)
                    {
                        return _ex;
                    }
                }
            }

            public void SetCompleted(object result)
            {
                lock (_syncObject)
                {
                    _result = result;
                    _isCompleted = true;
                    _waitHandle.Set();
                    if (_asyncCallback != null)
                    {
                        _asyncCallback(this);
                    }
                }
            }

            public void SetCompletedException(Exception ex)
            {
                lock (_syncObject)
                {
                    _ex = ex;
                    _isCompleted = true;
                    _waitHandle.Set();
                    if (_asyncCallback != null)
                    {
                        _asyncCallback(this);
                    }
                }
            }

        }

    }
}
