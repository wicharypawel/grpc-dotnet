namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    /// <summary>
    /// We will temporarily use the _experimental suffix while the policy is in development
    /// </summary>
    //internal sealed class EdsPolicyProvider : IGrpcLoadBalancingPolicyProvider
    //{
    //    public string PolicyName => "eds";
    //
    //    public int Priority => 5;
    //
    //    public bool IsAvailable => true;
    //
    //    public IGrpcLoadBalancingPolicy CreateLoadBalancingPolicy()
    //    {
    //        return new EdsPolicy();
    //    }
    //}

    internal sealed class EdsExperimentalPolicyProvider : IGrpcLoadBalancingPolicyProvider
    {
        public string PolicyName => "eds_experimental";

        public int Priority => 5;

        public bool IsAvailable => true;

        public IGrpcLoadBalancingPolicy CreateLoadBalancingPolicy()
        {
            return new EdsPolicy();
        }
    }
}
