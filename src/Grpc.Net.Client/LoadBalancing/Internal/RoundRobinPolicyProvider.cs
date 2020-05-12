namespace Grpc.Net.Client.LoadBalancing.Internal
{
    internal sealed class RoundRobinPolicyProvider : IGrpcLoadBalancingPolicyProvider
    {
        public string PolicyName => "round_robin";

        public int Priority => 5;

        public bool IsAvailable => true;

        public IGrpcLoadBalancingPolicy CreateLoadBalancingPolicy(IGrpcHelper helper)
        {
            return new RoundRobinPolicy(helper);
        }
    }
}
