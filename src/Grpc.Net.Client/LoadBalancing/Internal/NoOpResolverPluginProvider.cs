using System;

namespace Grpc.Net.Client.LoadBalancing.Internal
{
    internal sealed class NoOpResolverPluginProvider : IGrpcResolverPluginProvider
    {
        public string Scheme => string.Empty;

        public int Priority => 5;

        public bool IsAvailable => true;

        public IGrpcResolverPlugin CreateResolverPlugin(Uri target, GrpcAttributes attributes)
        {
            return new NoOpResolverPlugin(attributes);
        }
    }

    /// <summary>
    /// Moreover, register NoOpResolverPlugin for http scheme. Dns discovery will be performed by HttpClient.
    /// </summary>
    internal sealed class HttpResolverPluginProvider : IGrpcResolverPluginProvider
    {
        public string Scheme => "http";

        public int Priority => 5;

        public bool IsAvailable => true;

        public IGrpcResolverPlugin CreateResolverPlugin(Uri target, GrpcAttributes attributes)
        {
            if (!target.Scheme.Equals(Scheme, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(nameof(target));
            }
            return new NoOpResolverPlugin(attributes);
        }
    }

    /// <summary>
    /// Moreover, register NoOpResolverPlugin for https scheme. Dns discovery will be performed by HttpClient.
    /// </summary>
    internal sealed class HttpsResolverPluginProvider : IGrpcResolverPluginProvider
    {
        public string Scheme => "https";

        public int Priority => 5;

        public bool IsAvailable => true;

        public IGrpcResolverPlugin CreateResolverPlugin(Uri target, GrpcAttributes attributes)
        {
            if (!target.Scheme.Equals(Scheme, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(nameof(target));
            }
            return new NoOpResolverPlugin(attributes);
        }
    }
}
