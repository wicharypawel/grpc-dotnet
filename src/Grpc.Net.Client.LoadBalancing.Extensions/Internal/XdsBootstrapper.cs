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
    internal sealed class XdsBootstrapper : IXdsBootstrapper
    {
        private const string BootstrapPathEnvironmentVariable = "GRPC_XDS_BOOTSTRAP";
        private const string ClientFeatureDisableOverprovisioning = "envoy.lb.does_not_support_overprovisioning";

        public static XdsBootstrapper Instance = new XdsBootstrapper();
        
        private ILogger _logger = NullLogger.Instance;

        public ILoggerFactory LoggerFactory { set => _logger = value.CreateLogger<XdsBootstrapper>(); }

        private XdsBootstrapper()
        {
        }

        public XdsBootstrapInfo ReadBootstrap()
        {
            _logger.LogDebug($"XdsBootstrapper Start ReadBootstrap");
            var filePath = Environment.GetEnvironmentVariable(BootstrapPathEnvironmentVariable);
            if (filePath == null)
            {
                throw new InvalidOperationException($"XdsBootstrapper Environment variable {BootstrapPathEnvironmentVariable} not defined.");
            }
            _logger.LogDebug($"XdsBootstrapper will load bootstrap file using path: {filePath}");
            return ReadBootstrap(File.ReadAllText(filePath, Encoding.UTF8));
        }

        public XdsBootstrapInfo ReadBootstrap(string inlineBootstrapFile)
        {
            if (string.IsNullOrWhiteSpace(inlineBootstrapFile))
            {
                throw new InvalidOperationException($"XdsBootstrapper Empty bootstrap file");
            }
            return ParseConfig(inlineBootstrapFile);
        }

#pragma warning disable CS8604 // Possible null reference argument.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
        private XdsBootstrapInfo ParseConfig(string bootstrapFile)
        {
            _logger.LogDebug("XdsBootstrapper Reading bootstrap information");
            var bootstrapFileModel = JsonSerializer.Deserialize<BootstrapFileModel>(bootstrapFile);
            if (bootstrapFileModel.XdsServers == null)
            {
                throw new InvalidOperationException("XdsBootstrapper Invalid bootstrap: 'xds_servers' does not exist.");
            }
            _logger.LogDebug($"Configured with {bootstrapFileModel.XdsServers.Count} xDS servers");
            var servers = new List<XdsBootstrapInfo.ServerInfo>();
            foreach (var serverConfig in bootstrapFileModel.XdsServers)
            {
                if(serverConfig.ServerUri == null)
                {
                    throw new InvalidOperationException("XdsBootstrapper Invalid bootstrap: 'xds_servers' contains unknown server.");
                }
                _logger.LogDebug($"XdsBootstrapper xDS server URI: {serverConfig.ServerUri}");
                var channelCredentials = new List<XdsBootstrapInfo.ChannelCreds>();
                if(serverConfig.ChannelCreds != null)
                {
                    foreach (BootstrapFileModel.ChannelCredsModel channelCreds in serverConfig.ChannelCreds)
                    {
                        if(channelCreds?.Type == null)
                        {
                            throw new InvalidOperationException("XdsBootstrapper Invalid bootstrap: 'xds_servers' contains server with unknown type 'channel_creds'.");
                        }
                        _logger.LogDebug($"Channel credentials option: {channelCreds.Type}");
                        var credential = new XdsBootstrapInfo.ChannelCreds(type: channelCreds.Type, channelCreds.Config);
                        channelCredentials.Add(credential);
                    }
                }
                servers.Add(new XdsBootstrapInfo.ServerInfo(serverConfig.ServerUri, channelCredentials));
            }
            var node = new Node();
            if(bootstrapFileModel?.Node != null)
            {
                var nodeModel = bootstrapFileModel.Node;
                node.Id = nodeModel.Id ?? string.Empty;
                node.Cluster = nodeModel.Cluster ?? string.Empty;
                if(nodeModel.Metadata != null)
                {
                    node.Metadata = new Struct();
                    foreach (var key in nodeModel.Metadata.Fields.Keys)
                    {
                        node.Metadata.Fields.Add(key, ConvertToValue(nodeModel.Metadata.Fields[key]));
                    }
                }
                if(nodeModel.Locality != null)
                {
                    node.Locality = new Locality()
                    {
                        Region = nodeModel.Locality.Region ?? string.Empty,
                        Zone = nodeModel.Locality.Zone ?? string.Empty,
                        SubZone = nodeModel.Locality.SubZone ?? string.Empty,
                    };
                }
            }

            var buildVersion = GrpcBuildVersion.Instance;
#pragma warning disable CS0612 // Type or member is obsolete this behaviour was ported from java implementation
            node.BuildVersion = buildVersion.ToString();
#pragma warning restore CS0612 // Type or member is obsolete
            node.UserAgentName = buildVersion.UserAgent;
            node.UserAgentVersion = buildVersion.ImplementationVersion;
            node.ClientFeatures.Add(ClientFeatureDisableOverprovisioning);
            _logger.LogDebug("XdsBootstrapper created XdsBootstrapInfo");
            return new XdsBootstrapInfo(servers, node);
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
