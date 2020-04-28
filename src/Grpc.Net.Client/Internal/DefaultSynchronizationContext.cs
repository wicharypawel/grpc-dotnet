using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Grpc.Net.Client.Internal
{
    internal sealed class DefaultSynchronizationContext
    {
        private readonly Action<Exception> _uncaughtExceptionHandler;
        private readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();
        private string? _drainingThreadDescriptor;

        public DefaultSynchronizationContext(Action<Exception> uncaughtExceptionHandler)
        {
            _uncaughtExceptionHandler = uncaughtExceptionHandler ?? throw new ArgumentNullException(nameof(uncaughtExceptionHandler));
        }

        public void Drain()
        {
            do
            {
                var currentThreadDescriptor = $"{Thread.CurrentThread.ManagedThreadId}-{Task.CurrentId}";
                if (Interlocked.CompareExchange(ref _drainingThreadDescriptor, currentThreadDescriptor, null) != null)
                {
                    return;
                }
                try
                {
                    while (_queue.TryDequeue(out var action))
                    {
                        try
                        {
                            action();
                        }
                        catch (Exception ex)
                        {
                            _uncaughtExceptionHandler(ex);
                        }
                    }
                }
                finally
                {
                    _drainingThreadDescriptor = null;
                }
                // must check queue again here to catch any added prior to clearing drainingThread
            } while (!_queue.IsEmpty);
        }

        public void ExecuteLater(Action action)
        {
            _queue.Enqueue(action ?? throw new ArgumentNullException(nameof(action)));
        }

        public void Execute(Action action)
        {
            ExecuteLater(action);
            Drain();
        }

        public void ThrowIfNotInThisSynchronizationContext()
        {
            var currentThreadDescriptor = $"{Thread.CurrentThread.ManagedThreadId}-{Task.CurrentId}";
            if (currentThreadDescriptor != _drainingThreadDescriptor)
            {
                throw new InvalidOperationException("Not called from the SynchronizationContext");
            }
        }

        public ScheduledHandle Schedule(Action action, TimeSpan delay)
        {
            var tokenSource = new CancellationTokenSource();
            var synchronizationContext = this;
            var scheduledHandle = new ScheduledHandle(tokenSource);
            Task.Delay(delay).ContinueWith((_) => 
            {
                if (!tokenSource.Token.IsCancellationRequested)
                {
                    scheduledHandle.ConfirmStarted();
                    synchronizationContext.Execute(action);
                }
            });
            return scheduledHandle;
        }

        internal sealed class ScheduledHandle
        {
            private readonly CancellationTokenSource _tokenSource;
            private bool hasStarted = false;

            public ScheduledHandle(CancellationTokenSource tokenSource)
            {
                _tokenSource = tokenSource ?? throw new ArgumentNullException(nameof(tokenSource));
            }

            internal void ConfirmStarted()
            {
                hasStarted = true;
            }

            public void Cancel()
            {
                _tokenSource.Cancel();
            }

            public bool IsPending()
            {
                return !(hasStarted || _tokenSource.IsCancellationRequested);
            }
        }
    }
}
