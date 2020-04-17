using Envoy.Api.V2.Core;
using System.Collections.Generic;

namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    internal sealed class XdsBootstrapInfo
    {
        public IReadOnlyList<ServerInfo> Servers { get; }
        public Node Node { get; }

        public XdsBootstrapInfo(List<ServerInfo> servers, Node node)
        {
            Servers = servers;
            Node = node;
        }

        internal sealed class ServerInfo
        {
            public string ServerUri { get; }
            public IReadOnlyList<ChannelCreds> ChannelCredsList { get; }

            public ServerInfo(string serverUri, List<ChannelCreds> channelCredsList)
            {
                ServerUri = serverUri;
                ChannelCredsList = channelCredsList;
            }
        }

        internal sealed class ChannelCreds
        {
            public string Type { get; }
            public IReadOnlyDictionary<string, object> Config { get; }

            public ChannelCreds(string type, Dictionary<string, object> config)
            {
                Type = type;
                Config = config;
            }
        }
    }
}
