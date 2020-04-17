using Envoy.Api.V2.Core;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    internal interface IXdsBootstrapper
    {
        public BootstrapInfo ReadBootstrap();
        public BootstrapInfo ReadBootstrap(string inlineBootstrapFile);
    }

    internal sealed class XdsBootstrapper : IXdsBootstrapper
    {
        private const string BOOTSTRAP_PATH_SYS_ENV_VAR = "GRPC_XDS_BOOTSTRAP";
        private const string CLIENT_FEATURE_DISABLE_OVERPROVISIONING = "envoy.lb.does_not_support_overprovisioning";

        public static XdsBootstrapper Instance = new XdsBootstrapper();
        
        private ILogger _logger = NullLogger.Instance;

        private XdsBootstrapper()
        {
        }

        public BootstrapInfo ReadBootstrap()
        {
            _logger.LogDebug($"XdsBootstrapper start ReadBootstrap");
            var filePath = Environment.GetEnvironmentVariable(BOOTSTRAP_PATH_SYS_ENV_VAR);
            if (filePath == null)
            {
                throw new InvalidOperationException($"XdsBootstrapper Environment variable {BOOTSTRAP_PATH_SYS_ENV_VAR} not defined.");
            }
            _logger.LogDebug($"XdsBootstrapper will load bootstrap file using path: {filePath}");
            return ReadBootstrap(Encoding.UTF8.GetString(File.ReadAllBytes(filePath)));
        }

        public BootstrapInfo ReadBootstrap(string inlineBootstrapFile)
        {
            if (string.IsNullOrWhiteSpace(inlineBootstrapFile))
            {
                throw new InvalidOperationException($"XdsBootstrapper Empty bootstrap file");
            }
            return ParseConfig(inlineBootstrapFile);
        }

#pragma warning disable CS8604 // Possible null reference argument.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
        private BootstrapInfo ParseConfig(string rawData)
        {
            _logger.LogDebug("XdsBootstrapper Reading bootstrap information");
            var bootstrapFileModel = JsonSerializer.Deserialize<BootstrapFileModel>(rawData);
            if (bootstrapFileModel.XdsServers == null)
            {
                throw new InvalidOperationException("XdsBootstrapper Invalid bootstrap: 'xds_servers' does not exist.");
            }
            _logger.LogDebug($"Configured with {bootstrapFileModel.XdsServers.Count} xDS servers");
            var servers = new List<BootstrapInfo.ServerInfo>();
            foreach (var serverConfig in bootstrapFileModel.XdsServers)
            {
                if(serverConfig.ServerUri == null)
                {
                    throw new InvalidOperationException("XdsBootstrapper Invalid bootstrap: 'xds_servers' contains unknown server.");
                }
                _logger.LogDebug($"XdsBootstrapper xDS server URI: {serverConfig.ServerUri}");
                var channelCredsOptions = new List<BootstrapInfo.ChannelCreds>();
                if(serverConfig.ChannelCreds != null)
                {
                    foreach (BootstrapFileModel.ChannelCredsModel channelCreds in serverConfig.ChannelCreds)
                    {
                        if(channelCreds?.Type == null)
                        {
                            throw new InvalidOperationException("XdsBootstrapper Invalid bootstrap: 'xds_servers' contains server with unknown type 'channel_creds'.");
                        }
                        _logger.LogDebug($"Channel credentials option: {channelCreds.Type}");
                        var creds = new BootstrapInfo.ChannelCreds(type: channelCreds.Type, channelCreds.Config);
                        channelCredsOptions.Add(creds);
                    }
                }
                servers.Add(new BootstrapInfo.ServerInfo(serverConfig.ServerUri, channelCredsOptions));
            }
            var nodeBuilder = new Node();
            if(bootstrapFileModel?.Node != null)
            {
                var node = bootstrapFileModel.Node;
                nodeBuilder.Id = node.Id ?? string.Empty;
                nodeBuilder.Cluster = node.Cluster ?? string.Empty;
                if(node.Metadata != null)
                {
                    nodeBuilder.Metadata = new Struct();
                    foreach (var key in node.Metadata.Fields.Keys)
                    {
                        nodeBuilder.Metadata.Fields.Add(key, ConvertToValue(node.Metadata.Fields[key]));
                    }
                }
                if(node.Locality != null)
                {
                    nodeBuilder.Locality = new Locality()
                    {
                        Region = node.Locality.Region ?? string.Empty,
                        Zone = node.Locality.Zone ?? string.Empty,
                        SubZone = node.Locality.SubZone ?? string.Empty,
                    };
                }
            }

            var buildVersion = GrpcBuildVersion.Instance;
#pragma warning disable CS0612 // Type or member is obsolete this behaviour was ported from java implementation
            nodeBuilder.BuildVersion = buildVersion.ToString();
#pragma warning restore CS0612 // Type or member is obsolete
            nodeBuilder.UserAgentName = buildVersion.UserAgent;
            nodeBuilder.UserAgentVersion = buildVersion.ImplementationVersion;
            nodeBuilder.ClientFeatures.Add(CLIENT_FEATURE_DISABLE_OVERPROVISIONING);
            return new BootstrapInfo(servers, nodeBuilder);
        }

        private static Value ConvertToValue(object value)
        {
            if (value == null)
            {
                return new Value() { NullValue = NullValue.NullValue };
            }
            if(value is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.Number)
                {
                    return new Value() { NumberValue = jsonElement.GetDouble() };
                } 
                else if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    return new Value() { StringValue = value.ToString() };
                }
                else
                {
                    throw new InvalidOperationException("XdsBootstrapper unsupported type of value in dictionary");
                }
            }
            else
            {
                return new Value() { NullValue = NullValue.NullValue };
            }
        }
    }

    internal sealed class BootstrapInfo
    {
        public IReadOnlyList<ServerInfo> Servers { get; }
        public Node Node { get; }

        public BootstrapInfo(List<ServerInfo> servers, Node node)
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

    internal sealed class BootstrapFileModel
    {
        [JsonPropertyName("xds_servers")]
        public List<XdsServersModel>? XdsServers { get; set; }
        
        [JsonPropertyName("node")]
        public NodeModel? Node { get; set; }

        internal sealed class XdsServersModel
        {
            [JsonPropertyName("server_uri")]
            public string? ServerUri { get; set; }

            [JsonPropertyName("channel_creds")]
            public List<ChannelCredsModel>? ChannelCreds { get; set; }
        }

        internal sealed class ChannelCredsModel
        {
            [JsonPropertyName("type")]
            public string? Type { get; set; }

            [JsonPropertyName("config")]
            public Dictionary<string, object>? Config { get; set; }
        }

        internal sealed class NodeModel
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("cluster")]
            public string? Cluster { get; set; }

            [JsonPropertyName("metadata")]
            public StructModel? Metadata { get; set; }

            [JsonPropertyName("locality")]
            public LocalityModel? Locality { get; set; }
        }

        internal sealed class StructModel
        {
            [JsonPropertyName("fields")]
            public Dictionary<string, object>? Fields { get; set; }
        }

        internal sealed class LocalityModel
        {
            [JsonPropertyName("region")]
            public string? Region { get; set; }

            [JsonPropertyName("zone")]
            public string? Zone { get; set; }

            [JsonPropertyName("sub_zone")]
            public string? SubZone { get; set; }
        }
    }

    internal sealed class GrpcBuildVersion
    {
        public static GrpcBuildVersion Instance = new GrpcBuildVersion();
        public string UserAgent { get; }
        public string ImplementationVersion { get; }
        
        private GrpcBuildVersion()
        {
            var assemblyVersion = typeof(GrpcChannel)
                .Assembly
                .GetCustomAttributes<AssemblyFileVersionAttribute>()
                .FirstOrDefault();

            UserAgent = "grpc-dotnet";
            ImplementationVersion = assemblyVersion.Version;
        }

        public override string ToString()
        {
            return UserAgent + " " + ImplementationVersion;
        }
    }
}
