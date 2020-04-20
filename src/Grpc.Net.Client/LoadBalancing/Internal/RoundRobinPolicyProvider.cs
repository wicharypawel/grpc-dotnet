namespace Grpc.Net.Client.LoadBalancing.Internal
{
    internal sealed class RoundRobinPolicyProvider : IGrpcLoadBalancingPolicyProvider
    {
        public string PolicyName => "round_robin";

        public IGrpcLoadBalancingPolicy CreateLoadBalancingPolicy()
        {
            return new RoundRobinPolicy();
        }
    }
}
