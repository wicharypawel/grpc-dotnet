namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// GrpcAttributes well-known keys.
    /// </summary>
    public sealed class GrpcAttributesConstants
    {
        /// <summary>
        /// Key used to set default load balancing policy.
        /// </summary>
        public static readonly string DefaultLoadBalancingPolicy = "default-loadbalancing-policy";

        /// <summary>
        /// Used to attach load balance token to subchannel.
        /// </summary>
        public static readonly string SubChannelLoadBalanceToken = "subchannel-loadbalance-token";

        /// <summary>
        /// Used to override default DNS cache duration (TTL).
        /// </summary>
        public static readonly string DnsResolverNetworkTtlSeconds = "dns-resolver-network-ttl-seconds";
    }
}
