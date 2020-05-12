namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    internal sealed class GrpclbPolicyProvider : IGrpcLoadBalancingPolicyProvider
    {
        public string PolicyName => "grpclb";

        public int Priority => 5;

        public bool IsAvailable => true;

        public IGrpcLoadBalancingPolicy CreateLoadBalancingPolicy(IGrpcHelper helper)
        {
            return new GrpclbPolicy(helper);
        }
    }
}
