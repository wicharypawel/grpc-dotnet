namespace Grpc.Net.Client.LoadBalancing.Policies
{
    /// <summary>
    /// Method extensions for <seealso cref="LoadBalancingPolicyRegistry"/>.
    /// </summary>
    public static class LoadBalancingPolicyRegistryExtensions
    {
        /// <summary>
        /// Register grpclb provider in registry.
        /// </summary>
        /// <param name="registry">Registry instance.</param>
        public static void RegisterGrpclb(this LoadBalancingPolicyRegistry registry)
        {
            registry.Register(new GrpclbPolicyProvider());
        }

        /// <summary>
        /// Register grpclb provider in registry.
        /// </summary>
        /// <param name="registry">Registry instance.</param>
        public static void RegisterRoundRobin(this LoadBalancingPolicyRegistry registry)
        {
            registry.Register(new RoundRobinPolicyProvider());
        }
    }
}
