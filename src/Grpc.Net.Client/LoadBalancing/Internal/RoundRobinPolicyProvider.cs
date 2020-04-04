namespace Grpc.Net.Client.LoadBalancing.Internal
{
    internal sealed class RoundRobinPolicyProvider : ILoadBalancingPolicyProvider
    {
        public string PolicyName => "round_robin";

        public IGrpcLoadBalancingPolicy CreateLoadBalancingPolicy()
        {
            return new RoundRobinPolicy();
        }
    }
}
