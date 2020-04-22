using Envoy.Api.V2;
using Envoy.Api.V2.Core;
using Envoy.Api.V2.ListenerNS;
using Envoy.Api.V2.Route;
using Envoy.Config.Filter.Network.HttpConnectionManager.V2;
using Envoy.Config.Listener.V2;
using Google.Protobuf.WellKnownTypes;
using System.Collections.Generic;

namespace Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins.Factories
{
    internal static class XdsClientTestFactory
    {
        public static readonly string ADS_TYPE_URL_LDS = "type.googleapis.com/envoy.api.v2.Listener";
        public static readonly string ADS_TYPE_URL_RDS = "type.googleapis.com/envoy.api.v2.RouteConfiguration";
        public static readonly string ADS_TYPE_URL_CDS = "type.googleapis.com/envoy.api.v2.Cluster";
        public static readonly string ADS_TYPE_URL_EDS = "type.googleapis.com/envoy.api.v2.ClusterLoadAssignment";

        public static DiscoveryResponse BuildDiscoveryResponse(string versionInfo, List<Any> resources, string typeUrl, string nonce)
        {
            var result = new DiscoveryResponse()
            {
                VersionInfo = versionInfo,
                TypeUrl = typeUrl,
                Nonce = nonce
            };
            result.Resources.AddRange(resources);
            return result;
        }

        public static Listener BuildListener(string name, Any apiListener)
        {
            return new Listener()
            {
                Name = name,
                Address = new Address(),
                FilterChains = { new FilterChain() },
                ApiListener = new ApiListener() { ApiListener_ = apiListener }
            };
        }

        public static RouteConfiguration BuildRouteConfiguration(string name, List<VirtualHost> virtualHosts)
        {
            var result = new RouteConfiguration()
            {
                Name = name
            };
            result.VirtualHosts.AddRange(virtualHosts);
            return result;
        }

        public static VirtualHost BuildVirtualHost(List<string> domains, string clusterName)
        {
            var result = new VirtualHost()
            {
                Name = "virtualhost00.googleapis.com", // don't care
                Routes = {
                    new Route()
                    {
                        Route_ = new RouteAction(){ Cluster = "whatever cluster"},
                        Match = new RouteMatch(){ Prefix = "" }
                    },
                    new Route()
                    {
                        Route_ = new RouteAction(){ Cluster = clusterName},
                        Match = new RouteMatch(){ Prefix = "" }
                    }
                },
            };
            result.Domains.AddRange(domains);
            return result;
        }

        public static DiscoveryResponse BuildLdsResponseForCluster(string versionInfo, string host, string clusterName, string nonce)
        {
            var virtualHosts = new List<VirtualHost>()
            {
                BuildVirtualHost(new List<string>() { host }, clusterName)
            };
            var connectionManager = new HttpConnectionManager()
            {
                RouteConfig = BuildRouteConfiguration("route-foo.googleapis.com", virtualHosts)
            };
            var listeners = new List<Any>()
            {
                Any.Pack(BuildListener(host, Any.Pack(connectionManager)))
            };
            return BuildDiscoveryResponse(versionInfo, listeners, ADS_TYPE_URL_LDS, nonce);
        }

        public static DiscoveryResponse BuildLdsResponseForRdsResource(string versionInfo, string host, string routeConfigName, string nonce)
        {
            var rdsConfig = new Rds
            {
                ConfigSource = new ConfigSource() { Ads = new AggregatedConfigSource() },
                RouteConfigName = routeConfigName
            };
            var connectionManager = new HttpConnectionManager()
            {
                Rds = rdsConfig
            };
            var listeners = new List<Any>()
            {
                Any.Pack(BuildListener(host, Any.Pack(connectionManager)))
            };
            return BuildDiscoveryResponse(versionInfo, listeners, ADS_TYPE_URL_LDS, nonce);
        }

        public static DiscoveryResponse BuildRdsResponseForCluster(string versionInfo, string routeConfigName, string host, string clusterName, string nonce)
        {
            var virtualHosts = new List<VirtualHost>()
            {
                BuildVirtualHost(new List<string>() { host }, clusterName)
            };
            var routeConfigs = new List<Any>()
            {
                Any.Pack(BuildRouteConfiguration(routeConfigName, virtualHosts))
            };
            return BuildDiscoveryResponse(versionInfo, routeConfigs, ADS_TYPE_URL_RDS, nonce);
        }
    }
}
