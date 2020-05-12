namespace Grpc.Net.Client.LoadBalancing.Internal
{
    internal sealed class PickFirstPolicyProvider : IGrpcLoadBalancingPolicyProvider
    {
        public string PolicyName => "pick_first";

        public int Priority => 5;

        public bool IsAvailable => true;

        public IGrpcLoadBalancingPolicy CreateLoadBalancingPolicy(IGrpcHelper helper)
        {
            return new PickFirstPolicy(helper);
        }
    }
}
