﻿using System;

namespace Grpc.Net.Client.LoadBalancing.Extensions
{
    internal sealed class XdsResolverPluginProvider : IGrpcResolverPluginProvider
    {
        public string Scheme => "xds";

        public int Priority => 4;

        public bool IsAvailable => true;

        public IGrpcResolverPlugin CreateResolverPlugin(Uri target, GrpcAttributes attributes)
        {
            if (!target.Scheme.Equals(Scheme, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(nameof(target));
            }
            return new XdsResolverPlugin(attributes);
        }
    }
}
