namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    internal sealed class GrpclbPolicyProvider : IGrpcLoadBalancingPolicyProvider
    {
        public string PolicyName => "grpclb";

        public IGrpcLoadBalancingPolicy CreateLoadBalancingPolicy()
        {
            return new GrpclbPolicy();
        }
    }
}
