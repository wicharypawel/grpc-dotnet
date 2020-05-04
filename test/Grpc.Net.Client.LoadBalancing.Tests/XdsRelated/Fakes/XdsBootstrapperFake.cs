using Envoy.Api.V2.Core;
using Grpc.Net.Client.LoadBalancing.Extensions.Internal;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Grpc.Net.Client.LoadBalancing.Tests.XdsRelated.Fakes
{
    internal sealed class XdsBootstrapperFake : IXdsBootstrapper
    {
        private const string ClientFeatureDisableOverprovisioning = "envoy.lb.does_not_support_overprovisioning";

        public ILoggerFactory LoggerFactory { set => throw new NotImplementedException(); }

        public XdsBootstrapInfo ReadBootstrap()
        {
            var servers = new List<XdsBootstrapInfo.ServerInfo>()
            {
                new XdsBootstrapInfo.ServerInfo("test-server-uri.googleapis.com", new List<XdsBootstrapInfo.ChannelCreds>())
            };
            var node = new Node()
            {
                Id = Guid.NewGuid().ToString(),
                Cluster = string.Empty,
                Metadata = { Fields = {  } },
                Locality = new Locality()
                {
                    Region = "local-test-cluster",
                    Zone = "a",
                    SubZone = string.Empty
                },
#pragma warning disable CS0612 // Type or member is obsolete
                BuildVersion = "grpc-dotnet 0.8.7",
#pragma warning restore CS0612 // Type or member is obsolete
                UserAgentName = "grpc-dotnet",
                UserAgentVersion = "0.8.7",
                ClientFeatures = { ClientFeatureDisableOverprovisioning }
            };
            return new XdsBootstrapInfo(servers, node);
        }

        public XdsBootstrapInfo ReadBootstrap(string inlineBootstrapFile)
        {
            throw new NotImplementedException();
        }
    }
}
