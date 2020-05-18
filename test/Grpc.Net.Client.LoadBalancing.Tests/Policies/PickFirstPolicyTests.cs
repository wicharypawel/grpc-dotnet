using Grpc.Core;
using Grpc.Net.Client.LoadBalancing.Internal;
using Grpc.Net.Client.LoadBalancing.Tests.Policies.Factories;
using Grpc.Net.Client.LoadBalancing.Tests.Policies.Fakes;
using System;
using System.Collections.Generic;
using System.Linq;
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
            var helper = new HelperFake();
            using var policy = new PickFirstPolicy(helper);
            var hostsAddresses = GrpcHostAddressFactory.GetNameResolution(0, 2);
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var resolvedAddresses = new GrpcResolvedAddresses(hostsAddresses, config, GrpcAttributes.Empty);

            // Act
            // Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await policy.HandleResolvedAddressesAsync(resolvedAddresses, "", false);
            });
            Assert.Equal("serviceName not defined.", exception.Message);
            exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await policy.HandleResolvedAddressesAsync(resolvedAddresses, string.Empty, false);
            });
            Assert.Equal("serviceName not defined.", exception.Message);
        }

        [Fact]
        public async Task ForEmptyResolutionPassed_UsePickFirstPolicy_ThrowArgumentException()
        {
            // Arrange
            var helper = new HelperFake();
            using var policy = new PickFirstPolicy(helper);
            var hostsAddresses = GrpcHostAddressFactory.GetNameResolution(0, 0);
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var resolvedAddresses = new GrpcResolvedAddresses(hostsAddresses, config, GrpcAttributes.Empty);

            // Act
            // Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await policy.HandleResolvedAddressesAsync(resolvedAddresses, "sample-service.contoso.com", false);
            });
            Assert.Equal("resolutionResult must contain at least one non-blancer address.", exception.Message);
        }

        [Fact]
        public async Task ForBalancersResolutionOnly_UsePickFirstPolicy_ThrowArgumentException()
        {
            // Arrange
            var helper = new HelperFake();
            using var policy = new PickFirstPolicy(helper);
            var hostsAddresses = GrpcHostAddressFactory.GetNameResolution(2, 0);
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var resolvedAddresses = new GrpcResolvedAddresses(hostsAddresses, config, GrpcAttributes.Empty);

            // Act
            // Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await policy.HandleResolvedAddressesAsync(resolvedAddresses, "sample-service.contoso.com", false); // load balancers are ignored
            });
            Assert.Equal("resolutionResult must contain at least one non-blancer address.", exception.Message);
        }

        [Fact]
        public async Task ForResolutionResults_UsePickFirstPolicy_CreateAmmountSubChannels()
        {
            // Arrange
            var helper = new HelperFake();
            using var policy = new PickFirstPolicy(helper);
            var hostsAddresses = GrpcHostAddressFactory.GetNameResolution(0, 4);
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var resolvedAddresses = new GrpcResolvedAddresses(hostsAddresses, config, GrpcAttributes.Empty);

            // Act
            await policy.HandleResolvedAddressesAsync(resolvedAddresses, "sample-service.contoso.com", false);
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
            var helper = new HelperFake();
            using var policy = new PickFirstPolicy(helper);
            var hostsAddresses = new List<GrpcHostAddress>() // do not use GrpcHostAddressFactory
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
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var resolvedAddresses = new GrpcResolvedAddresses(hostsAddresses, config, GrpcAttributes.Empty);

            // Act
            await policy.HandleResolvedAddressesAsync(resolvedAddresses, "sample-service.contoso.com", true);
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
            var subChannels = GrpcSubChannelFactory.GetSubChannelsWithoutLoadBalanceTokens();
            using var picker = new PickFirstPolicy.Picker(subChannels[0]);

            // Act
            // Assert
            for (int i = 0; i < 30; i++)
            {
                var pickResult = picker.GetNextSubChannel();
                Assert.NotNull(pickResult);
                Assert.NotNull(pickResult!.SubChannel);
                Assert.Equal(subChannels[0].Address.Host, pickResult!.SubChannel!.Address.Host);
                Assert.Equal(subChannels[0].Address.Port, pickResult.SubChannel.Address.Port);
                Assert.Equal(subChannels[0].Address.Scheme, pickResult.SubChannel.Address.Scheme);
                Assert.Equal(Status.DefaultSuccess, pickResult.Status);
            }
        }
    }
}
