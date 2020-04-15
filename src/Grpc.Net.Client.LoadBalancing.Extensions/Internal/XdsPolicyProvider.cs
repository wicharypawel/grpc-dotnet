namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    internal sealed class XdsPolicyProvider : ILoadBalancingPolicyProvider
    {
        public string PolicyName => "xds";

        public IGrpcLoadBalancingPolicy CreateLoadBalancingPolicy()
        {
            return new XdsPolicy();
        }
    }
}
