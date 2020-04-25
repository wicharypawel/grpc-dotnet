#pragma warning disable CA1812 // Classes in this file are used for deserialization
using Google.Protobuf.WellKnownTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    // based on: https://github.com/grpc/proposal/blob/master/A2-service-configs-in-dns.md
    // based on: https://github.com/grpc/proposal/blob/master/A24-lb-policy-config.md
    internal sealed class GrpcConfigModel
    {
        public ServiceConfigModel ServiceConfig { get; set; } = new ServiceConfigModel();
    }

    //based on: https://github.com/grpc/grpc-proto/blob/master/grpc/service_config/service_config.proto
    internal sealed class ServiceConfigModel
    {
        // This field is deprecated but currently widely used
        public string LoadBalancingPolicy { get; set; } = string.Empty;
        public List<LoadBalancingConfig> LoadBalancingConfig { get; set; } = new List<LoadBalancingConfig>();
        public string[] GetLoadBalancingPolicies()
        {
            if (LoadBalancingConfig.Count != 0)
            {
                return LoadBalancingConfig.Select(x => x.GetPolicyName()).ToArray();
            }
            if (LoadBalancingPolicy != string.Empty)
            {
                return new string[] { LoadBalancingPolicy.ToLowerInvariant() };
            }
            else
            {
                throw new InvalidOperationException("Invalid ServiceConfig, load balancing policy must be specified.");
            }
        }
    }

    internal sealed class LoadBalancingConfig
    {
        [JsonPropertyName("pick_first")]
        public PickFirstConfig? PickFirst { get; set; }
        [JsonPropertyName("round_robin")]
        public RoundRobinConfig? RoundRobin { get; set; }
        public GrpcLbConfig? Grpclb { get; set; }
        public XdsConfig? Xds { get; set; }
        [JsonPropertyName("xds_experimental")]
        public XdsConfig? XdsExperimental { get; set; }
        public CdsConfig? Cds { get; set; }

        public string GetPolicyName()
        {
            // according to proto file only one configuration can be specified 
            return Grpclb?.ToString() ?? RoundRobin?.ToString() ?? PickFirst?.ToString()
                ?? Xds?.ToString() ?? XdsExperimental?.ToString() ?? Cds?.ToString()
                ?? throw new InvalidOperationException("Load balancing config without policy defined.");
        }
    }

    internal sealed class PickFirstConfig
    {
        //This should be left empty, see service_config.proto file

        public override string ToString()
        {
            return "pick_first";
        }
    }

    internal sealed class RoundRobinConfig
    {
        //This should be left empty, see service_config.proto file

        public override string ToString()
        {
            return "round_robin";
        }
    }

    internal sealed class GrpcLbConfig
    {
        public List<LoadBalancingConfig>? ChildPolicy { get; set; }

        public string ServiceName { get; set; } = string.Empty;

        public override string ToString()
        {
            return "grpclb";
        }
    }

    internal sealed class XdsConfig
    {
        public string BalancerName { get; set; } = string.Empty; // deprecated field
        public List<LoadBalancingConfig> ChildPolicy { get; set; } = new List<LoadBalancingConfig>();
        public List<LoadBalancingConfig> FallbackPolicy { get; set; } = new List<LoadBalancingConfig>();
        public string EdsServiceName { get; set; } = string.Empty;
        public StringValue? LrsLoadReportingServerName { get; set; }

        public override string ToString()
        {
            return "xds";
        }
    }

    internal sealed class CdsConfig
    {
        public string Cluster { get; set; } = string.Empty;

        public override string ToString()
        {
            return "cds";
        }
    }
}
