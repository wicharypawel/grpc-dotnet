using System.Linq;
using Xunit;
using static Grpc.Net.Client.LoadBalancing.Extensions.Internal.EnvoyServerProtoData;

namespace Grpc.Net.Client.LoadBalancing.Tests.Core
{
    public sealed class EnvoyServerProtoDataTests
    {
        [Fact]
        public void ForListener_UsingFromEnvoyProtoListener_ConvertToAndFromProto()
        {
            // Arrange
            var listener = new Envoy.Api.V2.Listener
            {
                Name = "8000",
                Address = new Envoy.Api.V2.Core.Address() 
                { 
                    SocketAddress = new Envoy.Api.V2.Core.SocketAddress() { Address = "102.10.2.12", PortValue = 8000 } 
                },
                FilterChains = 
                {
                    CreateTestOutFilter(),
                    CreateTestInFilter()
                }
            };

            // Act
            var xdsListener = Listener.FromEnvoyProtoListener(listener);

            // Assert
            Assert.NotNull(xdsListener);
            Assert.Equal("8000", xdsListener.Name);
            Assert.Equal("102.10.2.12:8000", xdsListener.Address);

            Assert.Equal(2, xdsListener.FilterChains.Count);
            Assert.Equal(8000, xdsListener.FilterChains.First().FilterChainMatch.DestinationPort);
            
            Assert.Equal(8000, xdsListener.FilterChains.Skip(1).First().FilterChainMatch.DestinationPort);
            Assert.Equal("10.20.0.15", xdsListener.FilterChains.Skip(1).First().FilterChainMatch.PrefixRanges.First().AddressPrefix);
            Assert.Equal(32, xdsListener.FilterChains.Skip(1).First().FilterChainMatch.PrefixRanges.First().PrefixLen);
            Assert.Equal("managed-mtls", xdsListener.FilterChains.Skip(1).First().FilterChainMatch.ApplicationProtocols.First());
        }

        private static Envoy.Api.V2.ListenerNS.FilterChain CreateTestOutFilter()
        {
            return new Envoy.Api.V2.ListenerNS.FilterChain()
            {
                FilterChainMatch = new Envoy.Api.V2.ListenerNS.FilterChainMatch()
                {
                    DestinationPort = 8000
                },
                Filters = 
                { 
                    new Envoy.Api.V2.ListenerNS.Filter() { Name = "envoy.http_connection_manager" }
                }
            };
        }
        private static Envoy.Api.V2.ListenerNS.FilterChain CreateTestInFilter()
        {
            return new Envoy.Api.V2.ListenerNS.FilterChain()
            {
                FilterChainMatch = new Envoy.Api.V2.ListenerNS.FilterChainMatch()
                {
                    DestinationPort = 8000,
                    PrefixRanges = 
                    { 
                        new Envoy.Api.V2.Core.CidrRange()
                        {
                            AddressPrefix = "10.20.0.15",
                            PrefixLen = 32
                        } 
                    },
                    ApplicationProtocols = { "managed-mtls" }
                },
                Filters =
                {
                    new Envoy.Api.V2.ListenerNS.Filter() 
                    { 
                        Name = "envoy.http_connection_manager", 
                        TypedConfig = new Google.Protobuf.WellKnownTypes.Any()
                        {
                            TypeUrl = "type.googleapis.com/envoy.config.filter.network.http_connection_manager.v2.HttpConnectionManager"
                        } 
                    }
                }
            };
        }
    }
}
