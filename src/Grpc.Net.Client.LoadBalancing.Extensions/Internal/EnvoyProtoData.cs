using System;
using System.Collections.Generic;
using System.Linq;

namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    /// <summary>
    /// This class wraps re-implementation of envoy types. Types are reimplemented 
    /// to avoid building implementation about elements that can change. Moreover 
    /// reimplemented types are much smaller in size as they do not store unused fields.
    /// </summary>
    internal static class EnvoyProtoData
    {
        /// <summary>
        /// See corresponding Envoy proto message <seealso cref="Envoy.Api.V2.Core.Locality"/>
        /// </summary>
        public sealed class Locality
        {
            public string Region { get; }
            public string Zone { get; }
            public string SubZone { get; }

            public Locality(string region, string zone, string subZone)
            {
                Region = region;
                Zone = zone;
                SubZone = subZone;
            }

            public static Locality FromEnvoyProtoLocality(Envoy.Api.V2.Core.Locality locality)
            {
                return new Locality(locality.Region, locality.Zone, locality.SubZone);
            }

            public Envoy.Api.V2.Core.Locality ToEnvoyProtoLocality()
            {
                return new Envoy.Api.V2.Core.Locality()
                {
                    Region = Region,
                    Zone = Zone,
                    SubZone = SubZone
                };
            }
        }

        /// <summary>
        /// See corresponding Envoy proto message <seealso cref="Envoy.Api.V2.Endpoint.LocalityLbEndpoints"/>
        /// </summary>
        public sealed class LocalityLbEndpoints
        {
            public IReadOnlyList<LbEndpoint> Endpoints { get; }
            public int LocalityWeight { get; }
            public int Priority { get; }

            public LocalityLbEndpoints(List<LbEndpoint> endpoints, int localityWeight, int priority)
            {
                Endpoints = endpoints;
                LocalityWeight = localityWeight;
                Priority = priority;
            }

            public static LocalityLbEndpoints FromEnvoyProtoLocalityLbEndpoints(Envoy.Api.V2.Endpoint.LocalityLbEndpoints localityLbEndpoints)
            {
                var endpoints = localityLbEndpoints.LbEndpoints.Select(LbEndpoint.FromEnvoyProtoLbEndpoint).ToList();
                return new LocalityLbEndpoints(endpoints, Convert.ToInt32(localityLbEndpoints.LoadBalancingWeight.GetValueOrDefault()), Convert.ToInt32(localityLbEndpoints.Priority));
            }
        }

        /// <summary>
        /// See corresponding Envoy proto message <seealso cref="Envoy.Api.V2.Endpoint.LbEndpoint"/>
        /// </summary>
        public sealed class LbEndpoint
        {
            public IReadOnlyList<GrpcHostAddress> HostsAddresses { get; }
            public int LoadBalancingWeight { get; }
            public bool IsHealthy { get; }

            public LbEndpoint(IReadOnlyList<GrpcHostAddress> hostsAddresses, int loadBalancingWeight, bool isHealthy)
            {
                HostsAddresses = hostsAddresses;
                LoadBalancingWeight = loadBalancingWeight;
                IsHealthy = isHealthy;
            }

            public static LbEndpoint FromEnvoyProtoLbEndpoint(Envoy.Api.V2.Endpoint.LbEndpoint endpoint)
            {
                var socketAddress = endpoint.Endpoint.Address.SocketAddress;
                var addr = new GrpcHostAddress(socketAddress.Address, Convert.ToInt32(socketAddress.PortValue));
                var loadBalancingWeight = Convert.ToInt32(endpoint.LoadBalancingWeight.GetValueOrDefault());
                var isHealthy = endpoint.HealthStatus == Envoy.Api.V2.Core.HealthStatus.Healthy || endpoint.HealthStatus == Envoy.Api.V2.Core.HealthStatus.Unknown;
                return new LbEndpoint(new List<GrpcHostAddress>() { addr }, loadBalancingWeight, isHealthy);
            }
        }

        /// <summary>
        /// See corresponding Envoy proto message <seealso cref="Envoy.Api.V2.ClusterLoadAssignment.Types.Policy.Types.DropOverload"/>
        /// </summary>
        public sealed class DropOverload
        {
            public string Category { get; }
            public int DropsPerMillion { get; }

            public DropOverload(string category, int dropsPerMillion)
            {
                Category = category;
                DropsPerMillion = dropsPerMillion;
            }

            public static DropOverload FromEnvoyProtoDropOverload(Envoy.Api.V2.ClusterLoadAssignment.Types.Policy.Types.DropOverload dropOverload)
            {
                var percent = dropOverload.DropPercentage;
                int numerator = Convert.ToInt32(percent.Numerator);
                switch (percent.Denominator)
                {
                    case Envoy.Type.FractionalPercent.Types.DenominatorType.TenThousand:
                        numerator *= 100;
                        break;
                    case Envoy.Type.FractionalPercent.Types.DenominatorType.Hundred:
                        numerator *= 100_00;
                        break;
                    case Envoy.Type.FractionalPercent.Types.DenominatorType.Million:
                        break;
                    default:
                        throw new InvalidOperationException("Unknown denominator type of " + percent);
                }
                if (numerator > 1_000_000)
                {
                    numerator = 1_000_000;
                }
                return new DropOverload(dropOverload.Category, numerator);
            }
        }

        /// <summary>
        /// See corresponding Envoy proto message <seealso cref="Envoy.Api.V2.Route.Route"/>
        /// </summary>
        public sealed class Route
        {
            public RouteMatch RouteMatch { get; }
            public RouteAction? RouteAction { get; }
            
            public Route(RouteMatch routeMatch, RouteAction? routeAction)
            {
                RouteMatch = routeMatch;
                RouteAction = routeAction;
            }

            public static Route FromEnvoyProtoRoute(Envoy.Api.V2.Route.Route route)
            {
                var routeMatch = RouteMatch.FromEnvoyProtoRouteMatch(route.Match);
                RouteAction? routeAction = route.Route_ == null ? null : RouteAction.FromEnvoyProtoRouteAction(route.Route_);
                return new Route(routeMatch, routeAction);
            }
        }

        /// <summary>
        /// See corresponding Envoy proto message <seealso cref="Envoy.Api.V2.Route.RouteMatch"/>
        /// </summary>
        public sealed class RouteMatch
        {
            public string Prefix { get; }
            public string Path { get; }
            public bool HasRegex { get; }
            public bool CaseSensitive { get; }

            public RouteMatch(string prefix, string path, bool hasRegex, bool caseSensitive)
            {
                Prefix = prefix;
                Path = path;
                HasRegex = hasRegex;
                CaseSensitive = caseSensitive;
            }

            public bool IsDefaultMatcher()
            {
                if (HasRegex)
                {
                    return false;
                }
                if (Path.Length != 0)
                {
                    return false;
                }
                return Prefix.Length == 0 || Prefix.Equals("/", StringComparison.Ordinal);
            }

            public static RouteMatch FromEnvoyProtoRouteMatch(Envoy.Api.V2.Route.RouteMatch routeMatch)
            {
#pragma warning disable CS0612 // Type or member is obsolete
                bool hasRegex = routeMatch.Regex.Length != 0 || routeMatch.SafeRegex != null;
#pragma warning restore CS0612 // Type or member is obsolete
                return new RouteMatch(routeMatch.Prefix, routeMatch.Path, hasRegex, routeMatch.CaseSensitive ?? true);
            }
        }

        /// <summary>
        /// See corresponding Envoy proto message <seealso cref="Envoy.Api.V2.Route.RouteAction"/>
        /// </summary>
        public sealed class RouteAction
        {
            public string Cluster { get; }
            public string ClusterHeader { get; }
            public IReadOnlyList<ClusterWeight> WeightedCluster { get; }

            public RouteAction(string cluster, string clusterHeader, List<ClusterWeight> weightedCluster)
            {
                Cluster = cluster;
                ClusterHeader = clusterHeader;
                WeightedCluster = weightedCluster;
            }

            public static RouteAction FromEnvoyProtoRouteAction(Envoy.Api.V2.Route.RouteAction routeAction)
            {
                var clusterWeights = routeAction.WeightedClusters.Clusters;
                return new RouteAction(routeAction.Cluster, routeAction.ClusterHeader,
                    clusterWeights.Select(ClusterWeight.FromEnvoyProtoClusterWeight).ToList());
            }
        }

        /// <summary>
        /// See corresponding Envoy proto message <seealso cref="Envoy.Api.V2.Route.WeightedCluster.Types.ClusterWeight"/>
        /// </summary>
        public sealed class ClusterWeight
        {
            public string Name { get; }
            public int Weight { get; }

            public ClusterWeight(string name, int weight)
            {
                Name = name;
                Weight = weight;
            }

            public static ClusterWeight FromEnvoyProtoClusterWeight(Envoy.Api.V2.Route.WeightedCluster.Types.ClusterWeight clusterWeight)
            {
                return new ClusterWeight(clusterWeight.Name, Convert.ToInt32(clusterWeight.Weight.GetValueOrDefault()));
            }
        }
    }
}
