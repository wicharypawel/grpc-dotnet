#region Copyright notice and license

// Copyright 2019 The gRPC Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using Grpc.Net.Client.LoadBalancing.Internal;
using Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins.Fakes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins
{
    public sealed class StaticResolverPluginTests
    {
        [Fact]
        public async Task ForStaticResolutionFunction_UseStaticResolverPlugin_ReturnPredefinedValues()
        {
            // Arrange
            Func<Uri, GrpcNameResolutionResult> resolveFunction = (uri) =>
            {
                var hosts = new List<GrpcHostAddress>()
                {
                    new GrpcHostAddress("10.1.5.212", 8080),
                    new GrpcHostAddress("10.1.5.213", 8080)
                };
                var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
                return new GrpcNameResolutionResult(hosts, config, GrpcAttributes.Empty);
            };
            var options = new StaticResolverPluginOptions(resolveFunction);
            var attributes = GrpcAttributes.Builder.NewBuilder().Add(GrpcAttributesConstants.StaticResolverOptions, options).Build();
            var resolverPlugin = new StaticResolverPlugin(attributes);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri("https://sample.host.com"), nameResolutionObserver);
            var resolutionResult = await nameResolutionObserver.GetFirstValueOrDefaultAsync();

            // Assert
            Assert.NotNull(resolutionResult);
            Assert.Equal(2, resolutionResult!.HostsAddresses.Count);
            Assert.Equal("10.1.5.212", resolutionResult.HostsAddresses[0].Host);
            Assert.Equal("10.1.5.213", resolutionResult.HostsAddresses[1].Host);
            Assert.Equal(8080, resolutionResult.HostsAddresses[0].Port);
            Assert.Equal(8080, resolutionResult.HostsAddresses[1].Port);
        }
    }
}
