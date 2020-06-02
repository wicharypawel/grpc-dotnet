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

using Grpc.Core;
using Grpc.Net.Client.LoadBalancing.Internal;
using Grpc.Net.Client.LoadBalancing.Tests.Core.Fakes;
using Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins.Factories;
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
        public void ForCallingMultipleSubscribe_UseStaticResolverPlugin_ThrowInvalidOperation()
        {
            // Arrange
            var executor = new ExecutorFake();
            var serviceHostName = "service.googleapis.com";
            var staticResolverOptions = new StaticResolverPluginOptions(GetSampleResolveFunction());
            var attributes = GrpcAttributes.Builder.NewBuilder()
                .Add(AttributesForResolverFactory.GetAttributes())
                .Add(GrpcAttributesConstants.StaticResolverOptions, staticResolverOptions).Build();
            using var resolverPlugin = new StaticResolverPlugin(attributes, executor);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            // Assert
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            Assert.Single(executor.Actions);
            Assert.Throws<InvalidOperationException>(() =>
            {
                resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            });
            Assert.Single(executor.Actions);
        }

        [Fact]
        public async Task ForUnsubscribeAndFollowingRefresh_UseStaticResolverPlugin_VerifyNotReturnResultsOnRefresh()
        {
            // Arrange
            var executor = new ExecutorFake();
            var serviceHostName = "service.googleapis.com";
            var staticResolverOptions = new StaticResolverPluginOptions(GetSampleResolveFunction());
            var attributes = GrpcAttributes.Builder.NewBuilder()
                .Add(AttributesForResolverFactory.GetAttributes())
                .Add(GrpcAttributesConstants.StaticResolverOptions, staticResolverOptions).Build();
            using var resolverPlugin = new StaticResolverPlugin(attributes, executor);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            executor.DrainSingleAction();
            var resolutionResult = await nameResolutionObserver.GetFirstValueOrDefaultAsync();
            Assert.NotNull(resolutionResult);
            resolverPlugin.Unsubscribe();

            // Assert
            Assert.Empty(executor.Actions);
            Assert.Throws<InvalidOperationException>(() =>
            {
                resolverPlugin.RefreshResolution();
            });
        }

        [Fact]
        public async Task ForStaticResolutionFunction_UseStaticResolverPlugin_ReturnPredefinedValues()
        {
            // Arrange
            var executor = new ExecutorFake();
            var serviceHostName = "service.googleapis.com";
            var staticResolverOptions = new StaticResolverPluginOptions(GetSampleResolveFunction());
            var attributes = GrpcAttributes.Builder.NewBuilder()
                .Add(AttributesForResolverFactory.GetAttributes())
                .Add(GrpcAttributesConstants.StaticResolverOptions, staticResolverOptions).Build();
            using var resolverPlugin = new StaticResolverPlugin(attributes, executor);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            executor.DrainSingleAction();
            var resolutionResult = await nameResolutionObserver.GetFirstValueOrDefaultAsync();

            // Assert
            Assert.NotNull(resolutionResult);
            Assert.Equal(2, resolutionResult!.HostsAddresses.Count);
            Assert.Equal("10.1.5.212", resolutionResult.HostsAddresses[0].Host);
            Assert.Equal("10.1.5.213", resolutionResult.HostsAddresses[1].Host);
            Assert.Equal(8080, resolutionResult.HostsAddresses[0].Port);
            Assert.Equal(8080, resolutionResult.HostsAddresses[1].Port);
        }

        [Fact]
        public void ForRefreshingResolution_UseStaticResolverPlugin_ReturnNextValues()
        {
            // Arrange
            var executor = new ExecutorFake();
            var serviceHostName = "service.googleapis.com";
            var staticResolverOptions = new StaticResolverPluginOptions(GetSampleResolveFunction());
            var attributes = GrpcAttributes.Builder.NewBuilder()
                .Add(AttributesForResolverFactory.GetAttributes())
                .Add(GrpcAttributesConstants.StaticResolverOptions, staticResolverOptions).Build();
            using var resolverPlugin = new StaticResolverPlugin(attributes, executor);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            // Assert
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            Assert.Single(executor.Actions);
            executor.DrainSingleAction();
            resolverPlugin.RefreshResolution();
            Assert.Single(executor.Actions);
            executor.DrainSingleAction();
            resolverPlugin.RefreshResolution();
            Assert.Single(executor.Actions);
            executor.DrainSingleAction();
            Assert.Empty(executor.Actions);
        }

        [Fact]
        public void ForRefreshingResolutionWhilePendingResolution_UseStaticResolverPlugin_ResolutionDoesNotOverlap()
        {
            // Arrange
            var executor = new ExecutorFake();
            var serviceHostName = "service.googleapis.com";
            var staticResolverOptions = new StaticResolverPluginOptions(GetSampleResolveFunction());
            var attributes = GrpcAttributes.Builder.NewBuilder()
                .Add(AttributesForResolverFactory.GetAttributes())
                .Add(GrpcAttributesConstants.StaticResolverOptions, staticResolverOptions).Build();
            using var resolverPlugin = new StaticResolverPlugin(attributes, executor);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            // Assert
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            Assert.Single(executor.Actions);
            resolverPlugin.RefreshResolution();
            resolverPlugin.RefreshResolution();
            resolverPlugin.RefreshResolution();
            Assert.Single(executor.Actions);
            executor.DrainSingleAction();
            Assert.Empty(executor.Actions);
        }

        [Fact]
        public async Task ForStaticResolutionFunctionWithException_UseStaticResolverPlugin_ReturnError()
        {
            // Arrange
            var executor = new ExecutorFake();
            var serviceHostName = "service.googleapis.com";
            var staticResolverOptions = new StaticResolverPluginOptions(GetSampleErrorResolveFunction());
            var attributes = GrpcAttributes.Builder.NewBuilder()
                .Add(AttributesForResolverFactory.GetAttributes())
                .Add(GrpcAttributesConstants.StaticResolverOptions, staticResolverOptions).Build();
            using var resolverPlugin = new StaticResolverPlugin(attributes, executor);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            executor.DrainSingleAction();
            var error = await nameResolutionObserver.GetFirstErrorOrDefaultAsync();

            // Assert
            Assert.Empty(executor.Actions);
            Assert.NotNull(error);
            Assert.Equal(StatusCode.Unavailable, error.Value.StatusCode);
        }

        private static Func<Uri, GrpcNameResolutionResult> GetSampleResolveFunction()
        {
            return new Func<Uri, GrpcNameResolutionResult>((uri) =>
            {
                var hosts = new List<GrpcHostAddress>()
                {
                    new GrpcHostAddress("10.1.5.212", 8080),
                    new GrpcHostAddress("10.1.5.213", 8080)
                };
                var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
                return new GrpcNameResolutionResult(hosts, config, GrpcAttributes.Empty);
            });
        }

        private static Func<Uri, GrpcNameResolutionResult> GetSampleErrorResolveFunction()
        {
            return new Func<Uri, GrpcNameResolutionResult>((uri) =>
            {
                throw new Exception("this is a test bug");
            });
        }
    }
}
