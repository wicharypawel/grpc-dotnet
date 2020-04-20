namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// Provider is responsible for creation of <seealso cref="IGrpcLoadBalancingPolicy"/>
    /// </summary>
    public interface IGrpcLoadBalancingPolicyProvider
    {
        /// <summary>
        /// Policy name written in snake_case eg. pick_first, round_robin, xds etc.
        /// </summary>
        public string PolicyName { get; }

        /// <summary>
        /// Factory method
        /// </summary>
        /// <returns>New instance of <seealso cref="IGrpcLoadBalancingPolicy"/></returns>
        public IGrpcLoadBalancingPolicy CreateLoadBalancingPolicy();
    }
}
