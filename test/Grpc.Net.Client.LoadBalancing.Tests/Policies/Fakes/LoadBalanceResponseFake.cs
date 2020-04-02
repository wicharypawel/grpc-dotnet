using Grpc.Core;
using Grpc.Lb.V1;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing.Tests.Policies.Fakes
{
    internal sealed class LoadBalanceResponseFake : IAsyncStreamReader<LoadBalanceResponse>
    {
        private readonly List<LoadBalanceResponse> _loadBalanceResponses;
        private int _streamIndex;

        public LoadBalanceResponseFake() : this(Array.Empty<LoadBalanceResponse>())
        {
        }

        public LoadBalanceResponseFake(IReadOnlyList<LoadBalanceResponse> loadBalanceResponses)
        {
            _loadBalanceResponses = new List<LoadBalanceResponse>(loadBalanceResponses);
            _streamIndex = -1;
        }

        public LoadBalanceResponse Current => _loadBalanceResponses[_streamIndex];

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            return Task.FromResult(++_streamIndex < _loadBalanceResponses.Count);
        }

        public LoadBalanceResponseFake AppendToEnd(LoadBalanceResponse response)
        {
            return AppendToEnd(response, 1);
        }

        public LoadBalanceResponseFake AppendToEnd(LoadBalanceResponse response, int count)
        {
            for (int i = 0; i < count; i++)
            {
                _loadBalanceResponses.Add(response);
            }
            return this;
        }

        public LoadBalanceResponseFake AppendToEnd(IReadOnlyList<LoadBalanceResponse> response)
        {
            _loadBalanceResponses.AddRange(response);
            return this;
        }
    }
}
