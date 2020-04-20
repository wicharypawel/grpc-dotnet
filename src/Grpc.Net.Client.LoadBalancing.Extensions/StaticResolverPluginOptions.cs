using System;

namespace Grpc.Net.Client.LoadBalancing.Extensions
{
    /// <summary>
    /// An options class for configuring a <see cref="StaticResolverPlugin"/>.
    /// </summary>
    public sealed class StaticResolverPluginOptions
    {
        /// <summary>
        /// Define name resolution using lambda.
        /// </summary>
        public Func<Uri, GrpcNameResolutionResult> StaticNameResolution { get; set; }

        /// <summary>
        /// Creates a new instance of <seealso cref="StaticResolverPluginOptions"/>.
        /// </summary>
        /// <param name="staticNameResolution">Define name resolution using lambda.</param>
        public StaticResolverPluginOptions(Func<Uri, GrpcNameResolutionResult> staticNameResolution)
        {
            StaticNameResolution = staticNameResolution;
        }
    }
}
