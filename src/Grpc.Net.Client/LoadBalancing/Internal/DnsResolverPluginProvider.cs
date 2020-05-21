using Grpc.Net.Client.Internal;
using System;

namespace Grpc.Net.Client.LoadBalancing.Internal
{
    internal sealed class DnsResolverPluginProvider : IGrpcResolverPluginProvider
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
            return new DnsResolverPlugin(attributes, new SystemTimer());
        }
    }
}
