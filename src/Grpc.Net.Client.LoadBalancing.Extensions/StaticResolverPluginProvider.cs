using System;

namespace Grpc.Net.Client.LoadBalancing.Extensions
{
    internal sealed class StaticResolverPluginProvider : IGrpcResolverPluginProvider
    {
        public string Scheme => "static";

        public IGrpcResolverPlugin CreateResolverPlugin(Uri target, GrpcAttributes attributes)
        {
            if (!target.Scheme.Equals(Scheme, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(nameof(target));
            }
            return new StaticResolverPlugin(attributes);
        }
    }
}
