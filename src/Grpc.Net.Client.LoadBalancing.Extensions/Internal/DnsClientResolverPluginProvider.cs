using System;

namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    internal sealed class DnsClientResolverPluginProvider : IGrpcResolverPluginProvider
    {
        public string Scheme => "dns";

        public int Priority => 5;

        public bool IsAvailable => true;

        public IGrpcResolverPlugin CreateResolverPlugin(Uri target, GrpcAttributes attributes)
        {
            if (!target.Scheme.Equals(Scheme, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(nameof(target));
            }
            return new DnsClientResolverPlugin(attributes);
        }
    }
}
