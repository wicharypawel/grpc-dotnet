using Grpc.Core;
using Grpc.Lb.V1;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing.Tests.Policies.Fakes
{
    internal sealed class LoadBalanceResponseFake : IAsyncStreamReader<LoadBalanceResponse>
    {
        private readonly IReadOnlyList<LoadBalanceResponse> _loadBalanceResponses;
        private int _streamIndex;

        public LoadBalanceResponseFake(IReadOnlyList<LoadBalanceResponse> loadBalanceResponses)
        {
            _loadBalanceResponses = loadBalanceResponses;
            _streamIndex = -1;
        }

        public LoadBalanceResponse Current => _loadBalanceResponses[_streamIndex];

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            return Task.FromResult(++_streamIndex < _loadBalanceResponses.Count);
        }
    }
}
