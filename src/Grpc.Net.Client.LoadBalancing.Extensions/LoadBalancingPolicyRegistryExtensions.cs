using Grpc.Net.Client.LoadBalancing.Extensions.Internal;

namespace Grpc.Net.Client.LoadBalancing.Extensions
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
    }
}
