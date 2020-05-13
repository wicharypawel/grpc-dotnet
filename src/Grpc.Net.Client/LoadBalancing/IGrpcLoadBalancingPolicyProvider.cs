namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// Provider is responsible for creation of <seealso cref="IGrpcLoadBalancingPolicy"/>.
    /// </summary>
    public interface IGrpcLoadBalancingPolicyProvider
    {
        /// <summary>
        /// Policy name written in snake_case eg. pick_first, round_robin, xds etc.
        /// 
        /// The policy name should consist of only lower case letters letters, underscore and digits,
        /// and can only start with letters. Policy name value shouldn't change.
        /// </summary>
        public string PolicyName { get; }

        /// <summary>
        /// A priority, from 0 to 10 that this provider should be used, taking the current environment into 
        /// consideration. 5 should be considered the default, and then tweaked based on environment 
        /// detection. A priority of 0 does not imply that the provider wouldn't work; just that 
        /// it should be last in line.
        /// 
        /// Priority is there in case there would be two providers defined for same policy name. 
        /// It is usefull if we want to override default provider.
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// Whether this provider is available for use, taking the current environment into consideration.
        /// If false, no other methods are safe to be called.
        /// </summary>
        public bool IsAvailable { get; }

        /// <summary>
        /// Factory method.
        /// </summary>
        /// <param name="helper">Channel helper instance.</param>
        /// <returns>New instance of <seealso cref="IGrpcLoadBalancingPolicy"/>.</returns>
        public IGrpcLoadBalancingPolicy CreateLoadBalancingPolicy(IGrpcHelper helper);
    }
}
