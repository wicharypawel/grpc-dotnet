namespace Grpc.Net.Client.LoadBalancing.Extensions
{
    /// <summary>
    /// GrpcAttributes well-known keys.
    /// </summary>
    public sealed class GrpcAttributesLbConstants
    {
        /// <summary>
        /// Key used to set options for static resolver.
        /// </summary>
        public static readonly string StaticResolverOptions = "static-resolver-options";

        /// <summary>
        /// Key used to set options for dns resolver.
        /// </summary>
        public static readonly string DnsResolverOptions = "dns-resolver-options";

        /// <summary>
        /// Key used to set options for xds resolver.
        /// </summary>
        public static readonly string XdsResolverOptions = "xds-resolver-options";
    }
}
