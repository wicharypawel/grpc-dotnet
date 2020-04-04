namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    internal sealed class GrpclbPolicyProvider : ILoadBalancingPolicyProvider
    {
        public string PolicyName => "grpclb";

        public IGrpcLoadBalancingPolicy CreateLoadBalancingPolicy()
        {
            return new GrpclbPolicy();
        }
    }
}
