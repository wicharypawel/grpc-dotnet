namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    internal sealed class XdsPolicyProvider : IGrpcLoadBalancingPolicyProvider
    {
        public string PolicyName => "xds";

        public int Priority => 5;

        public bool IsAvailable => true;

        public IGrpcLoadBalancingPolicy CreateLoadBalancingPolicy()
        {
            return new XdsPolicy();
        }
    }
}
