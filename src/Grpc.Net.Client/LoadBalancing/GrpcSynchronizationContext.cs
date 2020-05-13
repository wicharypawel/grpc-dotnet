using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// A synchronization context is a queue of tasks that run in sequence. It offers following
    /// guarantees:
    /// - Ordering. Tasks are run in the same order as they are submitted via <see cref="Execute(Action)"/>
    ///   and <see cref="ExecuteLater(Action)"/>.
    /// - Serialization. Tasks are run in sequence and establish a happens-before relationship
    ///   between them.
    /// - Non-reentrancy. If a task running in a synchronization context executes or schedules
    ///   another task in the same synchronization context, the latter task will never run
    ///   inline. It will instead be queued and run only after the current task has returned.
    ///   
    /// It doesn't own any thread. Tasks are run from caller's or caller-provided threads.
    /// 
    /// This class is thread-safe.
    /// </summary>
    public sealed class GrpcSynchronizationContext
    {
        private readonly Action<Exception> _uncaughtExceptionHandler;
        private readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();
        private string? _drainingThreadDescriptor;

        /// <summary>
        /// Creates a SynchronizationContext.
        /// </summary>
        /// <param name="uncaughtExceptionHandler">Handles exceptions thrown out of the tasks.</param>
        public GrpcSynchronizationContext(Action<Exception> uncaughtExceptionHandler)
        {
            _uncaughtExceptionHandler = uncaughtExceptionHandler ?? throw new ArgumentNullException(nameof(uncaughtExceptionHandler));
        }

        /// <summary>
        /// Run all tasks in the queue in the current thread, if no other thread is running this method.
        /// Otherwise do nothing.
        /// 
        /// Upon returning, it guarantees that all tasks submitted by <see cref="ExecuteLater(Action)"/> method before it
        /// have been or will eventually be run, while not requiring any more calls to <see cref="Drain"/> method.
        /// </summary>
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

        /// <summary>
        /// Adds a task that will be run when <see cref="Drain"/> method is called.
        /// 
        /// This is useful for cases where you want to enqueue a task while under a lock of your own,
        /// but don't want the tasks to be run under your lock (for fear of deadlock). You can call
        /// <see cref="ExecuteLater(Action)"/> method in the lock, and call <see cref="Drain"/> method outside the lock.
        /// </summary>
        /// <param name="action">The action.</param>
        public void ExecuteLater(Action action)
        {
            _queue.Enqueue(action ?? throw new ArgumentNullException(nameof(action)));
        }

        /// <summary>
        /// Adds a task and run it in this synchronization context as soon as possible. The task may run
        /// inline. If there are tasks that are previously queued by <see cref="ExecuteLater(Action)"/> but have not
        /// been run, this method will trigger them to be run before the given task. This is equivalent to
        /// calling <see cref="ExecuteLater(Action)"/> immediately followed by <see cref="Drain"/> method.
        /// </summary>
        /// <param name="action">The action.</param>
        public void Execute(Action action)
        {
            ExecuteLater(action);
            Drain();
        }

        /// <summary>
        /// Throw <see cref="InvalidOperationException"/> if this method is not called from this synchronization
        /// context.
        /// </summary>
        public void ThrowIfNotInThisSynchronizationContext()
        {
            var currentThreadDescriptor = $"{Thread.CurrentThread.ManagedThreadId}-{Task.CurrentId}";
            if (currentThreadDescriptor != _drainingThreadDescriptor)
            {
                throw new InvalidOperationException("Not called from the SynchronizationContext");
            }
        }

        /// <summary>
        /// Schedules a task to be added and run via <see cref="Execute(Action)"/> after a delay.
        /// </summary>
        /// <param name="action">The action being scheduled.</param>
        /// <param name="delay">The delay.</param>
        /// <returns>An object for checking the status and/or cancel the scheduled task.</returns>
        public ScheduledHandle Schedule(Action action, TimeSpan delay)
        {
            var synchronizationContext = this;
            var scheduledHandle = ScheduledHandle.Create(out var token);
            Task.Delay(delay, token).ContinueWith((_) =>
            {
                if (!token.IsCancellationRequested)
                {
                    scheduledHandle.ConfirmStarted();
                    synchronizationContext.Execute(action);
                }
            });
            return scheduledHandle;
        }

        /// <summary>
        /// Allows the user to check the status and/or cancel a task scheduled by <see cref="Schedule(Action, TimeSpan)"/>.
        /// 
        /// This class is NOT thread-safe.  All methods must be run from the same <see cref="GrpcSynchronizationContext"/>
        /// as which the task was scheduled in.
        /// </summary>
        public sealed class ScheduledHandle
        {
            private readonly CancellationTokenSource _tokenSource;
            private bool hasStarted = false;

            internal static ScheduledHandle Create(out CancellationToken token)
            {
                var result = new ScheduledHandle();
                token = result._tokenSource.Token;
                return result;
            }

            private ScheduledHandle()
            {
                _tokenSource = new CancellationTokenSource();
            }

            internal void ConfirmStarted()
            {
                hasStarted = true;
                _tokenSource.Dispose();
            }

            /// <summary>
            /// Cancel the task if it's still <see cref="IsPending"/>.
            /// </summary>
            public void Cancel()
            {
                if (!IsPending())
                {
                    return;
                }
                _tokenSource.Cancel();
                _tokenSource.Dispose();
            }

            /// <summary>
            /// Returns true if the task will eventually run, meaning that it has neither started
            /// running nor been cancelled.
            /// </summary>
            /// <returns></returns>
            public bool IsPending()
            {
                return !(hasStarted || _tokenSource.IsCancellationRequested);
            }

            /// <summary>
            /// Ensures that all unmanaged resources are released.
            /// </summary>
            ~ScheduledHandle()
            {
                _tokenSource.Dispose();
            }
        }
    }
}
