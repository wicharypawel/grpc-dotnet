namespace Grpc.Net.Client.LoadBalancing.Internal
{
    internal sealed class PickFirstPolicyProvider : ILoadBalancingPolicyProvider
    {
        public string PolicyName => "pick_first";

        public IGrpcLoadBalancingPolicy CreateLoadBalancingPolicy()
        {
            return new PickFirstPolicy();
        }
    }
}
