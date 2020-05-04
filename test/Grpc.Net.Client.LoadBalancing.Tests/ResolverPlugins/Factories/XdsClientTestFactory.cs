using Envoy.Api.V2;
using Envoy.Api.V2.Core;
using Envoy.Api.V2.Endpoint;
using Envoy.Api.V2.ListenerNS;
using Envoy.Api.V2.Route;
using Envoy.Config.Filter.Network.HttpConnectionManager.V2;
using Envoy.Config.Listener.V2;
using Google.Protobuf.WellKnownTypes;
using System;
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

        public static Cluster BuildCluster(string clusterName, string edsServiceName)
        {
            return new Cluster()
            {
                Type = Cluster.Types.DiscoveryType.Eds,
                EdsClusterConfig = new Cluster.Types.EdsClusterConfig() 
                { 
                    EdsConfig = new ConfigSource() { Ads = new AggregatedConfigSource() }, 
                    ServiceName = edsServiceName 
                },
                LbPolicy = Cluster.Types.LbPolicy.RoundRobin,
                Name = clusterName
            };
        }

        public static ClusterLoadAssignment BuildClusterLoadAssignment(string clusterName, LocalityLbEndpoints endpoints)
        {
            return new ClusterLoadAssignment() { ClusterName = clusterName, Endpoints = { endpoints } };
        }

        public static DiscoveryResponse BuildLdsResponseForCluster(string versionInfo, string host, string clusterName, string nonce)
        {
            //domain name should not have port
            var domainName = !host.Contains(':') ? host : host.Substring(0, host.IndexOf(':'));
            var virtualHosts = new List<VirtualHost>()
            {
                BuildVirtualHost(new List<string>() { domainName }, clusterName)
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

        public static DiscoveryResponse BuildMalformedLdsResponse(string versionInfo, string host, string nonce)
        {
            var connectionManager = new HttpConnectionManager();
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
            //domain name should not have port
            var domainName = !host.Contains(':') ? host : host.Substring(0, host.IndexOf(':'));
            var virtualHosts = new List<VirtualHost>()
            {
                BuildVirtualHost(new List<string>() { domainName }, clusterName)
            };
            var routeConfigs = new List<Any>()
            {
                Any.Pack(BuildRouteConfiguration(routeConfigName, virtualHosts))
            };
            return BuildDiscoveryResponse(versionInfo, routeConfigs, ADS_TYPE_URL_RDS, nonce);
        }

        public static DiscoveryResponse BuildCdsResponseForCluster(string versionInfo, string clusterName, string edsServiceName, string nonce)
        {
            var clusters = new List<Any>()
            {
                Any.Pack(BuildCluster(clusterName, edsServiceName))
            };
            return BuildDiscoveryResponse(versionInfo, clusters, ADS_TYPE_URL_CDS, nonce);
        }

        public static DiscoveryResponse BuildEdsResponseForCluster(string versionInfo, string clusterName, string nonce)
        {
            var endpoints = new LocalityLbEndpoints()
            {
                LbEndpoints =
                {
                    GetLbEndpoint("10.1.5.210", 80),
                    GetLbEndpoint("10.1.5.211", 80),
                    GetLbEndpoint("10.1.5.212", 80)
                },
                LoadBalancingWeight = 3,
                Priority = 1,
                Locality = new Locality()
                {
                    Region = "test-locality",
                    Zone = "a",
                    SubZone = string.Empty
                }
            };
            var clusterLoadAssignments = new List<Any>()
            {
                Any.Pack(BuildClusterLoadAssignment(clusterName, endpoints))
            };
            return BuildDiscoveryResponse(versionInfo, clusterLoadAssignments, ADS_TYPE_URL_EDS, nonce);
        }

        private static LbEndpoint GetLbEndpoint(string address, int port)
        {
            var endpoint = new LbEndpoint();
            endpoint.Endpoint = new Endpoint();
            endpoint.Endpoint.Address = new Address();
            endpoint.Endpoint.Address.SocketAddress = new SocketAddress();
            endpoint.Endpoint.Address.SocketAddress.Address = address;
            endpoint.Endpoint.Address.SocketAddress.PortValue = Convert.ToUInt32(port);
            endpoint.HealthStatus = HealthStatus.Unknown;
            endpoint.LoadBalancingWeight = 1;
            return endpoint;
        }
    }
}
