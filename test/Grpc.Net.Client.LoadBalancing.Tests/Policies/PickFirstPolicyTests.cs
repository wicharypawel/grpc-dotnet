using Grpc.Net.Client.LoadBalancing.Internal;
using Grpc.Net.Client.LoadBalancing.Tests.Policies.Factories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Policies
{
    public sealed class PickFirstPolicyTests
    {
        [Fact]
        public async Task ForEmptyServiceName_UsePickFirstPolicy_ThrowArgumentException()
        {
            // Arrange
            using var policy = new PickFirstPolicy();
            var resolutionResults = GrpcHostAddressFactory.GetNameResolution(0, 2);

            // Act
            // Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await policy.CreateSubChannelsAsync(resolutionResults, "", false);
            });
            Assert.Equal("serviceName not defined", exception.Message);
            exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await policy.CreateSubChannelsAsync(resolutionResults, string.Empty, false);
            });
            Assert.Equal("serviceName not defined", exception.Message);
        }

        [Fact]
        public async Task ForEmptyResolutionPassed_UsePickFirstPolicy_ThrowArgumentException()
        {
            // Arrange
            using var policy = new PickFirstPolicy();

            // Act
            // Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await policy.CreateSubChannelsAsync(new List<GrpcHostAddress>(), "sample-service.contoso.com", false);
            });
            Assert.Equal("resolutionResult must contain at least one non-blancer address", exception.Message);
        }

        [Fact]
        public async Task ForBalancersResolutionOnly_UsePickFirstPolicy_ThrowArgumentException()
        {
            // Arrange
            using var policy = new PickFirstPolicy();
            var resolutionResults = GrpcHostAddressFactory.GetNameResolution(2, 0);

            // Act
            // Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await policy.CreateSubChannelsAsync(resolutionResults, "sample-service.contoso.com", false); // load balancers are ignored
            });
            Assert.Equal("resolutionResult must contain at least one non-blancer address", exception.Message);
        }

        [Fact]
        public async Task ForResolutionResults_UsePickFirstPolicy_CreateAmmountSubChannels()
        {
            // Arrange
            using var policy = new PickFirstPolicy();
            var resolutionResults = GrpcHostAddressFactory.GetNameResolution(0, 4);

            // Act
            await policy.CreateSubChannelsAsync(resolutionResults, "sample-service.contoso.com", false);
            var subChannels = policy.SubChannels;

            // Assert
            Assert.Single(subChannels);
            Assert.Equal("http", subChannels[0].Address.Scheme);
            Assert.Equal(80, subChannels[0].Address.Port);
            Assert.StartsWith("10.1.5.210", subChannels[0].Address.Host);
        }

        [Fact]
        public async Task ForResolutionResultWithBalancers_UsePickFirstPolicy_IgnoreBalancersCreateSubchannels()
        {
            // Arrange
            using var policy = new PickFirstPolicy();
            var resolutionResults = new List<GrpcHostAddress>() // do not use GrpcHostAddressFactory
            {
                new GrpcHostAddress("10.1.6.120", 80)
                {
                    IsLoadBalancer = true
                },
                new GrpcHostAddress("10.1.5.212", 8443)
                {
                    IsLoadBalancer = false
                },
                new GrpcHostAddress("10.1.6.121", 80)
                {
                    IsLoadBalancer = true
                },
                new GrpcHostAddress("10.1.5.214", 8443)
                {
                    IsLoadBalancer = false
                }
            };

            // Act
            await policy.CreateSubChannelsAsync(resolutionResults, "sample-service.contoso.com", true);
            var subChannels = policy.SubChannels;

            // Assert
            Assert.Single(subChannels); // load balancers are ignored
            Assert.Equal("https", subChannels[0].Address.Scheme);
            Assert.Equal(8443, subChannels[0].Address.Port);
            Assert.StartsWith("10.1.5.212", subChannels[0].Address.Host);
        }

        [Fact]
        public void ForGrpcSubChannels_UsePickFirstPolicySelectChannels_SelectFirstChannel()
        {
            // Arrange
            using var policy = new PickFirstPolicy();
            var subChannels = GrpcSubChannelFactory.GetSubChannelsWithoutLoadBalanceTokens();
            policy.SubChannels = subChannels;

            // Act
            // Assert
            for (int i = 0; i < 30; i++)
            {
                var subChannel = policy.GetNextSubChannel();
                Assert.Equal(subChannels[0].Address.Host, subChannel.Address.Host);
                Assert.Equal(subChannels[0].Address.Port, subChannel.Address.Port);
                Assert.Equal(subChannels[0].Address.Scheme, subChannel.Address.Scheme);
            }
        }
    }
}
