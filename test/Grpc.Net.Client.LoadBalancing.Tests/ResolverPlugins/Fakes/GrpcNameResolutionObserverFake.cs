using Grpc.Core;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins.Fakes
{
    internal sealed class GrpcNameResolutionObserverFake : IGrpcNameResolutionObserver
    {
        private readonly ConcurrentQueue<GrpcNameResolutionResult> _results = new ConcurrentQueue<GrpcNameResolutionResult>();
        private readonly ConcurrentQueue<Status> _errors = new ConcurrentQueue<Status>();

        public void OnNext(GrpcNameResolutionResult value)
        {
            _results.Enqueue(value);
        }

        public void OnError(Status error)
        {
            _errors.Enqueue(error);
        }

        internal async Task<GrpcNameResolutionResult?> GetFirstValueOrDefaultAsync(TimeSpan? timeout = null)
        {
            var timeoutTask = Task.Delay(timeout ?? TimeSpan.FromSeconds(2));
            while (!timeoutTask.IsCompleted)
            {
                if (!_results.IsEmpty && _results.TryPeek(out var result))
                {
                    return result;
                }
                await Task.Delay(TimeSpan.FromMilliseconds(25));
            }
            return null;
        }

        internal async Task<Status?> GetFirstErrorOrDefaultAsync(TimeSpan? timeout = null)
        {
            var timeoutTask = Task.Delay(timeout ?? TimeSpan.FromSeconds(2));
            while (!timeoutTask.IsCompleted)
            {
                if (!_errors.IsEmpty && _errors.TryPeek(out var result))
                {
                    return result;
                }
                await Task.Delay(TimeSpan.FromMilliseconds(25));
            }
            return null;
        }
    }
}
