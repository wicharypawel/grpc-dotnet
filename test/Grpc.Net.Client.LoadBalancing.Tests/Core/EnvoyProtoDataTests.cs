using System;
using System.Linq;
using Xunit;
using static Grpc.Net.Client.LoadBalancing.Extensions.Internal.EnvoyProtoData;

namespace Grpc.Net.Client.LoadBalancing.Tests.Core
{
    public sealed class EnvoyProtoDataTests
    {
        [Fact]
        public void ForLocality_UsingFromEnvoyProtoLocality_ConvertToAndFromProto()
        {
            // Arrange
            var locality = new Envoy.Api.V2.Core.Locality
            {
                Region = "test_region",
                Zone = "test_zone",
                SubZone = "test_subzone"
            };

            // Act
            var xdsLocality = Locality.FromEnvoyProtoLocality(locality);
            Assert.Equal("test_region", xdsLocality.Region);
            Assert.Equal("test_zone", xdsLocality.Zone);
            Assert.Equal("test_subzone", xdsLocality.SubZone);
            var convertedLocality = xdsLocality.ToEnvoyProtoLocality();

            // Assert
            Assert.Equal("test_region", convertedLocality.Region);
            Assert.Equal("test_zone", convertedLocality.Zone);
            Assert.Equal("test_subzone", convertedLocality.SubZone);
        }

        [Fact]
        public void ForLocalityLbEndpoints_UsingFromEnvoyProtoLocalityLbEndpoints_ConvertFromProto()
        {
            // Arrange
            var localityLbEndpoints = new Envoy.Api.V2.Endpoint.LocalityLbEndpoints
            {
                LbEndpoints = { 
                    new Envoy.Api.V2.Endpoint.LbEndpoint()
                    {
                        HealthStatus = Envoy.Api.V2.Core.HealthStatus.Healthy,
                        LoadBalancingWeight = 1,
                        Endpoint = CreateTestEndpoint("102.1.23.10", 80)
                    },
                    new Envoy.Api.V2.Endpoint.LbEndpoint()
                    {
                        HealthStatus = Envoy.Api.V2.Core.HealthStatus.Healthy,
                        LoadBalancingWeight = 1,
                        Endpoint = CreateTestEndpoint("102.1.23.11", 80)
                    },
                    new Envoy.Api.V2.Endpoint.LbEndpoint()
                    {
                        HealthStatus = Envoy.Api.V2.Core.HealthStatus.Healthy,
                        LoadBalancingWeight = 1,
                        Endpoint = CreateTestEndpoint("102.1.23.12", 80)
                    }
                },
                LoadBalancingWeight = 3,
                Priority = 1,
            };

            // Act
            var xdsLocalityLbEndpoints = LocalityLbEndpoints.FromEnvoyProtoLocalityLbEndpoints(localityLbEndpoints);

            // Assert
            Assert.Equal(1, xdsLocalityLbEndpoints.Priority);
            Assert.Equal(3, xdsLocalityLbEndpoints.LocalityWeight);
            Assert.Equal(3, xdsLocalityLbEndpoints.Endpoints.Count);
            Assert.True(xdsLocalityLbEndpoints.Endpoints.All(x => x.IsHealthy));
            Assert.True(xdsLocalityLbEndpoints.Endpoints.All(x => x.LoadBalancingWeight == 1));
            Assert.True(xdsLocalityLbEndpoints.Endpoints.All(x => x.HostsAddresses != null));
            Assert.True(xdsLocalityLbEndpoints.Endpoints.All(x => x.HostsAddresses.All(y => y.Port == 80)));
            Assert.True(xdsLocalityLbEndpoints.Endpoints.All(x => x.HostsAddresses.All(y => y.Host.StartsWith("102.1.23.1"))));
        }

        private static Envoy.Api.V2.Endpoint.Endpoint CreateTestEndpoint(string address, int port)
        {
            return new Envoy.Api.V2.Endpoint.Endpoint()
            {
                Address = new Envoy.Api.V2.Core.Address() 
                { 
                    SocketAddress = new Envoy.Api.V2.Core.SocketAddress() { Address = address, PortValue = Convert.ToUInt32(port) } 
                }
            };
        }

        [Fact]
        public void ForLbEndpoints_UsingFromEnvoyProtoLbEndpoint_ConvertFromProto()
        {
            // Arrange
            var lbEndpoint = new Envoy.Api.V2.Endpoint.LbEndpoint()
            {
                Endpoint = CreateTestEndpoint("102.1.23.10", 80),
                LoadBalancingWeight = 2,
                HealthStatus = Envoy.Api.V2.Core.HealthStatus.Unhealthy
            };

            // Act
            var xdsLbEndpoint = LbEndpoint.FromEnvoyProtoLbEndpoint(lbEndpoint);

            // Assert
            Assert.Equal(2, xdsLbEndpoint.LoadBalancingWeight);
            Assert.False(xdsLbEndpoint.IsHealthy);
            Assert.Equal(80, xdsLbEndpoint.HostsAddresses.First().Port);
            Assert.Equal("102.1.23.10", xdsLbEndpoint.HostsAddresses.First().Host);
        }

        [Fact]
        public void ForRouteMatch_UsingFromEnvoyProtoRouteMatch_RouteMatchCaseSensitive()
        {
            Assert.True(RouteMatch.FromEnvoyProtoRouteMatch(new Envoy.Api.V2.Route.RouteMatch()).CaseSensitive); // if not set, default to true
            Assert.True(RouteMatch.FromEnvoyProtoRouteMatch(new Envoy.Api.V2.Route.RouteMatch() { CaseSensitive = true }).CaseSensitive);
            Assert.False(RouteMatch.FromEnvoyProtoRouteMatch(new Envoy.Api.V2.Route.RouteMatch() { CaseSensitive = false }).CaseSensitive);
        }

        [Theory]
        [InlineData(1, Envoy.Type.FractionalPercent.Types.DenominatorType.Million, 1)]
        [InlineData(1, Envoy.Type.FractionalPercent.Types.DenominatorType.TenThousand, 100)]
        [InlineData(1, Envoy.Type.FractionalPercent.Types.DenominatorType.Hundred, 10000)]
        [InlineData(10, Envoy.Type.FractionalPercent.Types.DenominatorType.Hundred, 100000)]
        [InlineData(1000, Envoy.Type.FractionalPercent.Types.DenominatorType.Hundred, 1000000)]
        public void ForDropOverload_UsingFromEnvoyProtoDropOverload_ConvertFromProto(uint numerator, 
            Envoy.Type.FractionalPercent.Types.DenominatorType denominator, int expectedDropResult)
        {
            // Arrange
            var dropOverload = new Envoy.Api.V2.ClusterLoadAssignment.Types.Policy.Types.DropOverload()
            {
                Category = "test-category",
                DropPercentage = new Envoy.Type.FractionalPercent()
                {
                    Numerator = numerator,
                    Denominator = denominator
                }
            };

            // Act
            var xdsDropOverload = DropOverload.FromEnvoyProtoDropOverload(dropOverload);

            // Assert
            Assert.Equal("test-category", xdsDropOverload.Category);
            Assert.Equal(expectedDropResult, xdsDropOverload.DropsPerMillion);
        }

        [Fact]
        public void ForRoute_UsingFromEnvoyProtoRoute_ConvertFromProto()
        {
            // Arrange
            var route = new Envoy.Api.V2.Route.Route()
            {
                Route_ = new Envoy.Api.V2.Route.RouteAction()
                {
                    WeightedClusters = new Envoy.Api.V2.Route.WeightedCluster()
                    {
                        Clusters =
                        {
                            new Envoy.Api.V2.Route.WeightedCluster.Types.ClusterWeight()
                            {
                                Name = "cluster-name-1",
                                Weight = 1
                            },
                            new Envoy.Api.V2.Route.WeightedCluster.Types.ClusterWeight()
                            {
                                Name = "cluster-name-2",
                                Weight = 1
                            }
                        }
                    }
                },
                Match = new Envoy.Api.V2.Route.RouteMatch()
                {
                    SafeRegex = new Envoy.Type.Matcher.RegexMatcher(),
                    Prefix = "",
                    Path = "test-path",
                    CaseSensitive = false
                }
            };

            // Act
            var xdsRoute = Route.FromEnvoyProtoRoute(route);

            // Assert
            Assert.NotNull(xdsRoute.RouteAction);
            Assert.NotNull(xdsRoute.RouteMatch);
            Assert.False(xdsRoute.RouteMatch.CaseSensitive);
            Assert.Equal("test-path", xdsRoute.RouteMatch.Path);
            Assert.Equal("", xdsRoute.RouteMatch.Prefix);
            Assert.NotNull(xdsRoute.RouteAction);
            Assert.Equal("", xdsRoute.RouteAction!.Cluster);
            Assert.Equal("", xdsRoute.RouteAction.ClusterHeader);
            Assert.Equal(2, xdsRoute.RouteAction.WeightedCluster.Count);
            Assert.True(xdsRoute.RouteAction.WeightedCluster.All(x => x.Name.StartsWith("cluster-name-") && x.Weight == 1));
        }

        [Fact]
        public void ForRouteWithClusterName_UsingFromEnvoyProtoRoute_ConvertFromProto()
        {
            // Arrange
            var route = new Envoy.Api.V2.Route.Route()
            {
                Route_ = new Envoy.Api.V2.Route.RouteAction()
                {
                    Cluster = "cluster-name"
                },
                Match = new Envoy.Api.V2.Route.RouteMatch()
                {
                    Prefix = "",
                    Path = "test-path-2",
                    CaseSensitive = true
                }
            };

            // Act
            var xdsRoute = Route.FromEnvoyProtoRoute(route);

            // Assert
            Assert.NotNull(xdsRoute.RouteAction);
            Assert.NotNull(xdsRoute.RouteMatch);
            Assert.True(xdsRoute.RouteMatch.CaseSensitive);
            Assert.Equal("test-path-2", xdsRoute.RouteMatch.Path);
            Assert.Equal("", xdsRoute.RouteMatch.Prefix);
            Assert.NotNull(xdsRoute.RouteAction);
            Assert.Equal("cluster-name", xdsRoute.RouteAction!.Cluster);
            Assert.Equal("", xdsRoute.RouteAction.ClusterHeader);
            Assert.Equal(0, xdsRoute.RouteAction.WeightedCluster.Count);
        }
    }
}
