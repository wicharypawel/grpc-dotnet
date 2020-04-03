namespace Grpc.Net.Client.LoadBalancing.Policies
{
    /// <summary>
    /// Provider is responsible for creation of <seealso cref="IGrpcLoadBalancingPolicy"/>
    /// </summary>
    internal sealed class GrpclbPolicyProvider : ILoadBalancingPolicyProvider
    {
        /// <summary>
        /// Policy name written in snake_case eg. pick_first, round_robin, xds etc.
        /// </summary>
        public string PolicyName => "grpclb";

        /// <summary>
        /// Factory method
        /// </summary>
        /// <returns>New instance of <seealso cref="IGrpcLoadBalancingPolicy"/></returns>
        public IGrpcLoadBalancingPolicy CreateLoadBalancingPolicy()
        {
            return new GrpclbPolicy();
        }
    }
}
