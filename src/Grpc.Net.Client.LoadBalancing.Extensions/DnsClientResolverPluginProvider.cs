using System;

namespace Grpc.Net.Client.LoadBalancing.Extensions
{
    internal sealed class DnsClientResolverPluginProvider : IGrpcResolverPluginProvider
    {
        public string Scheme => "dns";

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
                                                                                                        