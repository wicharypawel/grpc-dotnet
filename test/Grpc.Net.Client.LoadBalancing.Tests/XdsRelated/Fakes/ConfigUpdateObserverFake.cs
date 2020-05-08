using Grpc.Core;
using Grpc.Net.Client.LoadBalancing.Extensions.Internal;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing.Tests.XdsRelated.Fakes
{
    internal sealed class ConfigUpdateObserverFake : IConfigUpdateObserver
    {
        private readonly ConcurrentQueue<ConfigUpdate> _results = new ConcurrentQueue<ConfigUpdate>();
        private readonly ConcurrentQueue<Status> _errors = new ConcurrentQueue<Status>();

        public void OnNext(ConfigUpdate value)
        {
            _results.Enqueue(value);
        }

        public void OnError(Status error)
        {
            _errors.Enqueue(error);
        }

        internal async Task<ConfigUpdate?> GetFirstValueOrDefaultAsync(TimeSpan? timeout = null)
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
