using DnsClient;
using DnsClient.Protocol;
using Grpc.Net.Client.LoadBalancing.ResolverPlugins;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins
{
    public sealed class DnsClientResolverPluginTests
    {
        [Fact]
        public async Task ForTargetWithNonDnsScheme_UseDnsClientResolverPlugin_ThrowArgumentException()
        {
            // Arrange
            var resolverPlugin = new DnsClientResolverPlugin();

            // Act
            // Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await resolverPlugin.StartNameResolutionAsync(new Uri("http://sample.host.com"));
            });
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await resolverPlugin.StartNameResolutionAsync(new Uri("https://sample.host.com"));
            });
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await resolverPlugin.StartNameResolutionAsync(new Uri("unknown://sample.host.com"));
            });
        }

        [Fact]
        public async Task ForTargetAndEmptyDnsResults_UseDnsClientResolverPlugin_ReturnNoFinidings()
        {
            // Arrange
            var serviceHostName = "my-service";
            var txtDnsQueryResponse = new Mock<IDnsQueryResponse>(MockBehavior.Strict);
            var srvBalancersDnsQueryResponse = new Mock<IDnsQueryResponse>(MockBehavior.Strict);
            var aServersDnsQueryResponse = new Mock<IDnsQueryResponse>(MockBehavior.Strict);
            var dnsClientMock = new Mock<IDnsQuery>(MockBehavior.Strict);

            txtDnsQueryResponse.Setup(x => x.Answers).Returns(new List<TxtRecord>().AsReadOnly());
            srvBalancersDnsQueryResponse.Setup(x => x.Answers).Returns(new List<SrvRecord>().AsReadOnly());
            aServersDnsQueryResponse.Setup(x => x.Answers).Returns(new List<ARecord>().AsReadOnly());

            dnsClientMock.Setup(x => x.QueryAsync($"_grpc_config.{serviceHostName}", QueryType.TXT, QueryClass.IN, default))
                .Returns(Task.FromResult(txtDnsQueryResponse.Object));
            dnsClientMock.Setup(x => x.QueryAsync($"_grpclb._tcp.{serviceHostName}", QueryType.SRV, QueryClass.IN, default))
                .Returns(Task.FromResult(srvBalancersDnsQueryResponse.Object));
            dnsClientMock.Setup(x => x.QueryAsync(serviceHostName, QueryType.A, QueryClass.IN, default))
                .Returns(Task.FromResult(aServersDnsQueryResponse.Object));

            var resolverPlugin = new DnsClientResolverPlugin();
            resolverPlugin.OverrideDnsClient = dnsClientMock.Object;

            // Act
            var resolutionResult = await resolverPlugin.StartNameResolutionAsync(new Uri($"dns://{serviceHostName}:80"));
            var serviceConfig = await resolverPlugin.GetServiceConfigAsync();

            // Assert
            Assert.Empty(resolutionResult);
            Assert.True(serviceConfig.RequestedLoadBalancingPolicies.Count == 1);
            Assert.True(serviceConfig.RequestedLoadBalancingPolicies.First() == "pick_first");
        }

        [Fact]
        public async Task ForTargetAndBalancerSrvRecordsConfigured_UseDnsClientResolverPlugin_ReturnServersAndBalancers()
        {
            // Arrange
            var serviceHostName = "my-service";
            var txtDnsQueryResponse = new Mock<IDnsQueryResponse>(MockBehavior.Strict);
            var srvBalancersDnsQueryResponse = new Mock<IDnsQueryResponse>(MockBehavior.Strict);
            var aServersDnsQueryResponse = new Mock<IDnsQueryResponse>(MockBehavior.Strict);
            var dnsClientMock = new Mock<IDnsQuery>(MockBehavior.Strict);

            txtDnsQueryResponse.Setup(x => x.Answers).Returns(new List<TxtRecord>().AsReadOnly());
            srvBalancersDnsQueryResponse.Setup(x => x.Answers).Returns(new List<SrvRecord>(GetBalancersSrvRecords(serviceHostName)).AsReadOnly());
            aServersDnsQueryResponse.Setup(x => x.Answers).Returns(new List<ARecord>(GetServersARecords(serviceHostName)).AsReadOnly());

            dnsClientMock.Setup(x => x.QueryAsync($"_grpc_config.{serviceHostName}", QueryType.TXT, QueryClass.IN, default))
                .Returns(Task.FromResult(txtDnsQueryResponse.Object));
            dnsClientMock.Setup(x => x.QueryAsync($"_grpclb._tcp.{serviceHostName}", QueryType.SRV, QueryClass.IN, default))
                .Returns(Task.FromResult(srvBalancersDnsQueryResponse.Object));
            dnsClientMock.Setup(x => x.QueryAsync(serviceHostName, QueryType.A, QueryClass.IN, default))
                .Returns(Task.FromResult(aServersDnsQueryResponse.Object));

            var resolverPlugin = new DnsClientResolverPlugin(new DnsClientResolverPluginOptions() { EnableSrvGrpclb = true });
            resolverPlugin.OverrideDnsClient = dnsClientMock.Object;

            // Act
            var resolutionResult = await resolverPlugin.StartNameResolutionAsync(new Uri($"dns://{serviceHostName}:443"));
            var serviceConfig = await resolverPlugin.GetServiceConfigAsync();

            // Assert
            Assert.Equal(5, resolutionResult.Count);
            Assert.Equal(2, resolutionResult.Where(x => x.IsLoadBalancer).Count());
            Assert.All(resolutionResult.Where(x => x.IsLoadBalancer), x => Assert.Equal(80, x.Port));
            Assert.All(resolutionResult.Where(x => x.IsLoadBalancer), x => Assert.StartsWith("10-1-6-", x.Host));
            Assert.Equal(3, resolutionResult.Where(x => !x.IsLoadBalancer).Count());
            Assert.All(resolutionResult.Where(x => !x.IsLoadBalancer), x => Assert.Equal(443, x.Port));
            Assert.All(resolutionResult.Where(x => !x.IsLoadBalancer), x => Assert.StartsWith("10.1.5.", x.Host));
            Assert.True(serviceConfig.RequestedLoadBalancingPolicies.First() == "grpclb");
        }

        [Fact]
        public async Task ForTargetAndBalancerAndSrvLookupDisabled_UseDnsClientResolverPlugin_ReturnOnlyServers()
        {
            // Arrange
            var serviceHostName = "my-service";
            var txtDnsQueryResponse = new Mock<IDnsQueryResponse>(MockBehavior.Strict);
            var srvBalancersDnsQueryResponse = new Mock<IDnsQueryResponse>(MockBehavior.Strict);
            var aServersDnsQueryResponse = new Mock<IDnsQueryResponse>(MockBehavior.Strict);
            var dnsClientMock = new Mock<IDnsQuery>(MockBehavior.Strict);

            txtDnsQueryResponse.Setup(x => x.Answers).Returns(new List<TxtRecord>().AsReadOnly());
            srvBalancersDnsQueryResponse.Setup(x => x.Answers).Returns(new List<SrvRecord>().AsReadOnly());
            aServersDnsQueryResponse.Setup(x => x.Answers).Returns(new List<ARecord>(GetServersARecords(serviceHostName)).AsReadOnly());

            dnsClientMock.Setup(x => x.QueryAsync($"_grpc_config.{serviceHostName}", QueryType.TXT, QueryClass.IN, default))
                .Returns(Task.FromResult(txtDnsQueryResponse.Object));
            dnsClientMock.Setup(x => x.QueryAsync($"_grpclb._tcp.{serviceHostName}", QueryType.SRV, QueryClass.IN, default))
                .Returns(Task.FromResult(srvBalancersDnsQueryResponse.Object));
            dnsClientMock.Setup(x => x.QueryAsync(serviceHostName, QueryType.A, QueryClass.IN, default))
                .Returns(Task.FromResult(aServersDnsQueryResponse.Object));

            var resolverPluginOptions = new DnsClientResolverPluginOptions() { EnableSrvGrpclb = false, EnableTxtServiceConfig = false };
            var resolverPlugin = new DnsClientResolverPlugin(resolverPluginOptions);
            resolverPlugin.OverrideDnsClient = dnsClientMock.Object;

            // Act
            var resolutionResult = await resolverPlugin.StartNameResolutionAsync(new Uri($"dns://{serviceHostName}:443"));
            var serviceConfig = await resolverPlugin.GetServiceConfigAsync();

            // Assert
            Assert.Equal(3, resolutionResult.Count);
            Assert.Empty(resolutionResult.Where(x => x.IsLoadBalancer));
            Assert.Equal(3, resolutionResult.Where(x => !x.IsLoadBalancer).Count());
            Assert.All(resolutionResult.Where(x => !x.IsLoadBalancer), x => Assert.Equal(443, x.Port));
            Assert.All(resolutionResult.Where(x => !x.IsLoadBalancer), x => Assert.StartsWith("10.1.5.", x.Host));
            Assert.True(serviceConfig.RequestedLoadBalancingPolicies.Count == 1);
            Assert.True(serviceConfig.RequestedLoadBalancingPolicies.First() == "pick_first");
        }

        [Fact]
        public async Task ForServiceConfigAndNoBalancers_UseDnsClientResolverPlugin_ReturnServersAndBalancersServiceConfig()
        {
            // Arrange
            var serviceHostName = "my-service";
            var txtDnsQueryResponse = new Mock<IDnsQueryResponse>(MockBehavior.Strict);
            var srvBalancersDnsQueryResponse = new Mock<IDnsQueryResponse>(MockBehavior.Strict);
            var aServersDnsQueryResponse = new Mock<IDnsQueryResponse>(MockBehavior.Strict);
            var dnsClientMock = new Mock<IDnsQuery>(MockBehavior.Strict);

            txtDnsQueryResponse.Setup(x => x.Answers).Returns(new List<TxtRecord>(GetServiceConfigTxtRecords(serviceHostName)).AsReadOnly());
            srvBalancersDnsQueryResponse.Setup(x => x.Answers).Returns(new List<SrvRecord>(GetBalancersSrvRecords(serviceHostName)).AsReadOnly());
            aServersDnsQueryResponse.Setup(x => x.Answers).Returns(new List<ARecord>(GetServersARecords(serviceHostName)).AsReadOnly());

            dnsClientMock.Setup(x => x.QueryAsync($"_grpc_config.{serviceHostName}", QueryType.TXT, QueryClass.IN, default))
                .Returns(Task.FromResult(txtDnsQueryResponse.Object));
            dnsClientMock.Setup(x => x.QueryAsync($"_grpclb._tcp.{serviceHostName}", QueryType.SRV, QueryClass.IN, default))
                .Returns(Task.FromResult(srvBalancersDnsQueryResponse.Object));
            dnsClientMock.Setup(x => x.QueryAsync(serviceHostName, QueryType.A, QueryClass.IN, default))
                .Returns(Task.FromResult(aServersDnsQueryResponse.Object));

            var resolverPluginOptions = new DnsClientResolverPluginOptions() { EnableSrvGrpclb = true, EnableTxtServiceConfig = true };
            var resolverPlugin = new DnsClientResolverPlugin(resolverPluginOptions);
            resolverPlugin.OverrideDnsClient = dnsClientMock.Object;

            // Act
            var resolutionResult = await resolverPlugin.StartNameResolutionAsync(new Uri($"dns://{serviceHostName}:443"));
            var serviceConfig = await resolverPlugin.GetServiceConfigAsync();

            // Assert
            Assert.Equal(5, resolutionResult.Count);
            Assert.Equal(2, resolutionResult.Where(x => x.IsLoadBalancer).Count());
            Assert.All(resolutionResult.Where(x => x.IsLoadBalancer), x => Assert.Equal(80, x.Port));
            Assert.All(resolutionResult.Where(x => x.IsLoadBalancer), x => Assert.StartsWith("10-1-6-", x.Host));
            Assert.Equal(3, resolutionResult.Where(x => !x.IsLoadBalancer).Count());
            Assert.All(resolutionResult.Where(x => !x.IsLoadBalancer), x => Assert.Equal(443, x.Port));
            Assert.All(resolutionResult.Where(x => !x.IsLoadBalancer), x => Assert.StartsWith("10.1.5.", x.Host));
            Assert.True(serviceConfig.RequestedLoadBalancingPolicies.First() == "round_robin");
        }

        private List<SrvRecord> GetBalancersSrvRecords(string serviceHostName)
        {
            return new List<SrvRecord>()
            {
                new SrvRecord(new ResourceRecordInfo($"_grpclb._tcp.{serviceHostName}", ResourceRecordType.SRV, QueryClass.IN, 30, 0), 0, 0, 80, DnsString.Parse($"10-1-6-120.{serviceHostName}")),
                new SrvRecord(new ResourceRecordInfo($"_grpclb._tcp.{serviceHostName}", ResourceRecordType.SRV, QueryClass.IN, 30, 0), 0, 0, 80, DnsString.Parse($"10-1-6-121.{serviceHostName}"))
            };
        }

        private List<ARecord> GetServersARecords(string serviceHostName)
        {
            return new List<ARecord>()
            {
                new ARecord(new ResourceRecordInfo(serviceHostName, ResourceRecordType.A, QueryClass.IN, 30, 0), IPAddress.Parse("10.1.5.211")),
                new ARecord(new ResourceRecordInfo(serviceHostName, ResourceRecordType.A, QueryClass.IN, 30, 0), IPAddress.Parse("10.1.5.212")),
                new ARecord(new ResourceRecordInfo(serviceHostName, ResourceRecordType.A, QueryClass.IN, 30, 0), IPAddress.Parse("10.1.5.213"))
            };
        }

        private List<TxtRecord> GetServiceConfigTxtRecords(string serviceHostName)
        {
            return new List<TxtRecord>()
            {
                new TxtRecord(new ResourceRecordInfo(serviceHostName, ResourceRecordType.TXT, QueryClass.IN, 30, 0), 
                    new string[] { TXT }, new string[] { TXT }),
            };
        }

        private static string TXT = @"grpc_config=[{""serviceConfig"":{""loadBalancingPolicy"":""round_robin"",""methodConfig"":[{""name"":[{""service"":""MyService"",""method"":""Foo""}],""waitForReady"":true}]}}]";
    }
}
