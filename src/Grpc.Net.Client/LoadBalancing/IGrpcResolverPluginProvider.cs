using System;

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// Provider is responsible for creation of <seealso cref="IGrpcResolverPlugin"/>.
    /// </summary>
    public interface IGrpcResolverPluginProvider
    {
        /// <summary>
        /// Scheme used for target written eg. http, dns, xds etc.
        /// </summary>
        public string Scheme { get; }

        /// <summary>
        /// Factory method.
        /// </summary>
        /// <param name="target">Target uri address. Uri scheme must match provider scheme.</param>
        /// <param name="attributes">Attributes for resolver plugin.</param>
        /// <returns>New instance of <seealso cref="IGrpcResolverPlugin"/>.</returns>
        public IGrpcResolverPlugin CreateResolverPlugin(Uri target, GrpcAttributes attributes);
    }
}
