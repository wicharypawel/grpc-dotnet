namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    /// <summary>
    /// We will temporarily use the _experimental suffix while the policy is in development
    /// </summary>
    //internal sealed class CdsPolicyProvider : IGrpcLoadBalancingPolicyProvider
    //{
    //    public string PolicyName => "cds";
    //
    //    public int Priority => 5;
    //
    //    public bool IsAvailable => true;
    //
    //    public IGrpcLoadBalancingPolicy CreateLoadBalancingPolicy()
    //    {
    //        return new CdsPolicy();
    //    }
    //}

    internal sealed class CdsExperimentalPolicyProvider : IGrpcLoadBalancingPolicyProvider
    {
        public string PolicyName => "cds_experimental";

        public int Priority => 5;

        public bool IsAvailable => true;

        public IGrpcLoadBalancingPolicy CreateLoadBalancingPolicy()
        {
            return new CdsPolicy();
        }
    }
}
