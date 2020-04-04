using System;
using System.Threading;
using Grpc.Core;
using Grpc.Lb.V1;

namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal.Abstraction
{
    /// <summary>
    /// This class wrap and delegate LoadBalancerClient.
    /// The reason why it was added described here <seealso cref="ILoadBalancerClient"/>  
    /// </summary>
    internal sealed class WrappedLoadBalancerClient : ILoadBalancerClient
    {
        private readonly GrpcChannel _channelForLB;
        private readonly LoadBalancer.LoadBalancerClient _loadBalancerClient;
        internal bool Disposed { get; private set; }

        public WrappedLoadBalancerClient(string address, GrpcChannelOptions channelOptionsForLB)
        {
            _channelForLB = GrpcChannel.ForAddress(address, channelOptionsForLB);
            _loadBalancerClient = new LoadBalancer.LoadBalancerClient(_channelForLB);
        }

        public IAsyncDuplexStreamingCall<LoadBalanceRequest, LoadBalanceResponse> BalanceLoad(CallOptions options)
        {
            return new WrappedAsyncDuplexStreamingCall<LoadBalanceRequest, LoadBalanceResponse>(_loadBalancerClient.BalanceLoad(options));
        }

        public IAsyncDuplexStreamingCall<LoadBalanceRequest, LoadBalanceResponse> BalanceLoad(Metadata? headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default)
        {
            return new WrappedAsyncDuplexStreamingCall<LoadBalanceRequest, LoadBalanceResponse>(_loadBalancerClient.BalanceLoad(headers, deadline, cancellationToken));
        }

        public void Dispose()
        {
            if (Disposed)
            {
                return;
            }
            _channelForLB.ShutdownAsync().Wait();
            _channelForLB.Dispose();
            Disposed = true;
        }
    }
}
