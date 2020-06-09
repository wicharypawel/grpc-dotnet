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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Core
{
    public sealed class GrpcNameResolutionObserverTests
    {
        [Fact]
        public void ForResolutionResultAndLbHandle_UseGrpcNameResolutionObserver_VerifySuccess()
        {
            // Arrange
            GrpcResolutionState? actualState = null;
            var channelMock = CreateGrpcChannelMock(MockBehavior.Loose);
            channelMock.SetupGet(x => x.LastResolutionState).Returns(GrpcResolutionState.NoResolution);
            channelMock.SetupSet(x => x.LastResolutionState = It.IsAny<GrpcResolutionState>()).Callback<GrpcResolutionState>((state) => { actualState = state; });
            channelMock.Setup(x => x.TryHandleResolvedAddresses(It.IsAny<GrpcResolvedAddresses>())).Returns(new Status(StatusCode.OK, string.Empty));
            var channel = channelMock.Object;
            var observer = new GrpcNameResolutionObserver(channel);

            // Act
            observer.OnNext(CreateSampleGrpcNameResolutionResult());

            // Assert
            Assert.Equal(GrpcResolutionState.Success, actualState);
            channelMock.Verify(x => x.TryHandleResolvedAddresses(It.IsAny<GrpcResolvedAddresses>()), Times.Once);
            channelMock.Verify(x => x.HandleNameResolutionError(It.IsAny<Status>()), Times.Never);
            channelMock.VerifySet(x => x.NameResolverRefreshBackoffPolicy = null, Times.Once);
        }

        [Fact]
        public void ForResolutionResultAndLbNotHandle_UseGrpcNameResolutionObserver_VerifyFailure()
        {
            // Arrange
            GrpcResolutionState? actualState = null;
            var channelMock = CreateGrpcChannelMock(MockBehavior.Loose);
            channelMock.SetupGet(x => x.LastResolutionState).Returns(GrpcResolutionState.NoResolution);
            channelMock.SetupSet(x => x.LastResolutionState = It.IsAny<GrpcResolutionState>()).Callback<GrpcResolutionState>((state) => { actualState = state; });
            channelMock.Setup(x => x.TryHandleResolvedAddresses(It.IsAny<GrpcResolvedAddresses>())).Returns(new Status(StatusCode.Internal, "test bug"));
            channelMock.SetupGet(x => x.NameResolverRefreshSchedule).Returns(GrpcSynchronizationContext.ScheduledHandle.Create(out _));
            var channel = channelMock.Object;
            var observer = new GrpcNameResolutionObserver(channel);

            // Act
            observer.OnNext(CreateSampleGrpcNameResolutionResult());

            // Assert
            Assert.Equal(GrpcResolutionState.Error, actualState);
            channelMock.Verify(x => x.TryHandleResolvedAddresses(It.IsAny<GrpcResolvedAddresses>()), Times.Once);
            channelMock.Verify(x => x.HandleNameResolutionError(It.IsAny<Status>()), Times.Once);
            channelMock.VerifySet(x => x.NameResolverRefreshBackoffPolicy = null, Times.Once);
            channelMock.VerifySet(x => x.NameResolverRefreshSchedule = It.IsAny<GrpcSynchronizationContext.ScheduledHandle?>(), Times.Never);
        }

        [Fact]
        public void ForResolutionResultError_UseGrpcNameResolutionObserver_VerifyFailure()
        {
            // Arrange
            GrpcResolutionState? actualState = null;
            var channelMock = CreateGrpcChannelMock(MockBehavior.Loose);
            channelMock.SetupGet(x => x.LastResolutionState).Returns(GrpcResolutionState.NoResolution);
            channelMock.SetupSet(x => x.LastResolutionState = It.IsAny<GrpcResolutionState>()).Callback<GrpcResolutionState>((state) => { actualState = state; });
            channelMock.SetupGet(x => x.NameResolverRefreshSchedule).Returns(GrpcSynchronizationContext.ScheduledHandle.Create(out _));
            var channel = channelMock.Object;
            var observer = new GrpcNameResolutionObserver(channel);

            // Act
            observer.OnError(new Status(StatusCode.Internal, "test bug"));

            // Assert
            Assert.Equal(GrpcResolutionState.Error, actualState);
            channelMock.Verify(x => x.TryHandleResolvedAddresses(It.IsAny<GrpcResolvedAddresses>()), Times.Never);
            channelMock.Verify(x => x.HandleNameResolutionError(It.IsAny<Status>()), Times.Once);
            channelMock.VerifySet(x => x.NameResolverRefreshBackoffPolicy = null, Times.Never);
            channelMock.VerifySet(x => x.NameResolverRefreshSchedule = It.IsAny<GrpcSynchronizationContext.ScheduledHandle?>(), Times.Never);
        }

        [Fact]
        public void ForResolutionResultErrorAndNoPendingSchedule_UseGrpcNameResolutionObserver_VerifyResolutionWasScheduledWithBackoff()
        {
            // Arrange
            var backoffPolicyMock = new Mock<IGrpcBackoffPolicy>();
            var backoffPolicyProviderMock = new Mock<IGrpcBackoffPolicyProvider>();
            backoffPolicyProviderMock.Setup(x => x.CreateBackoffPolicy()).Returns(backoffPolicyMock.Object);
            var channelMock = CreateGrpcChannelMock(MockBehavior.Loose);
            channelMock.SetupGet(x => x.LastResolutionState).Returns(GrpcResolutionState.Success);
            channelMock.SetupGet(x => x.NameResolverRefreshSchedule).Returns((GrpcSynchronizationContext.ScheduledHandle?)null);
            var i = 0;
            channelMock.SetupGet(x => x.NameResolverRefreshBackoffPolicy).Returns(() => i++ == 0 ? null : backoffPolicyMock.Object);
            channelMock.SetupGet(x => x.BackoffPolicyProvider).Returns(backoffPolicyProviderMock.Object);
            var channel = channelMock.Object;
            var observer = new GrpcNameResolutionObserver(channel);

            // Act
            observer.OnError(new Status(StatusCode.Internal, "test bug"));

            // Assert
            channelMock.Verify(x => x.TryHandleResolvedAddresses(It.IsAny<GrpcResolvedAddresses>()), Times.Never);
            channelMock.Verify(x => x.HandleNameResolutionError(It.IsAny<Status>()), Times.Once);
            channelMock.VerifySet(x => x.NameResolverRefreshBackoffPolicy = It.IsAny<IGrpcBackoffPolicy?>(), Times.Once);
            channelMock.VerifySet(x => x.NameResolverRefreshSchedule = It.IsAny<GrpcSynchronizationContext.ScheduledHandle?>(), Times.AtLeastOnce());
            backoffPolicyMock.Verify(x => x.NextBackoff(), Times.Once);
        }

        [Fact]
        public void ForResolutionResultErrorBeingOkStatus_UseGrpcNameResolutionObserver_ThrowsArgumentException()
        {
            // Arrange
            var channelMock = CreateGrpcChannelMock(MockBehavior.Loose);
            var channel = channelMock.Object;
            var observer = new GrpcNameResolutionObserver(channel);

            // Act
            // Assert
            var error = Assert.Throws<ArgumentException>(() => 
            {
                observer.OnError(new Status(StatusCode.OK, string.Empty));
            });
            Assert.Equal("The error status must not be OK.", error.Message);
        }

        [Fact]
        public void ForResolutionResultAndShutdownChannel_UseGrpcNameResolutionObserver_VerifyNoProcessing()
        {
            // Arrange
            GrpcResolutionState? actualState = null;
            var channelMock = CreateGrpcChannelMock(MockBehavior.Loose);
            channelMock.SetupGet(x => x.LastResolutionState).Returns(GrpcResolutionState.NoResolution);
            channelMock.SetupSet(x => x.LastResolutionState = It.IsAny<GrpcResolutionState>()).Callback<GrpcResolutionState>((state) => { actualState = state; });
            channelMock.Setup(x => x.TryHandleResolvedAddresses(It.IsAny<GrpcResolvedAddresses>())).Returns(new Status(StatusCode.OK, string.Empty));
            channelMock.SetupGet(x => x.IsShutdown).Returns(true);
            var channel = channelMock.Object;
            var observer = new GrpcNameResolutionObserver(channel);

            // Act
            observer.OnNext(CreateSampleGrpcNameResolutionResult());

            // Assert
            channelMock.Verify(x => x.TryHandleResolvedAddresses(It.IsAny<GrpcResolvedAddresses>()), Times.Never);
            channelMock.Verify(x => x.HandleNameResolutionError(It.IsAny<Status>()), Times.Never);
        }

        [Fact]
        public void ForResolutionErrorAndShutdownChannel_UseGrpcNameResolutionObserver_VerifyNoProcessing()
        {
            // Arrange
            GrpcResolutionState? actualState = null;
            var channelMock = CreateGrpcChannelMock(MockBehavior.Loose);
            channelMock.SetupGet(x => x.LastResolutionState).Returns(GrpcResolutionState.NoResolution);
            channelMock.SetupSet(x => x.LastResolutionState = It.IsAny<GrpcResolutionState>()).Callback<GrpcResolutionState>((state) => { actualState = state; });
            channelMock.SetupGet(x => x.IsShutdown).Returns(true);
            var channel = channelMock.Object;
            var observer = new GrpcNameResolutionObserver(channel);

            // Act
            observer.OnError(new Status(StatusCode.Internal, "test bug"));

            // Assert
            channelMock.Verify(x => x.TryHandleResolvedAddresses(It.IsAny<GrpcResolvedAddresses>()), Times.Never);
            channelMock.Verify(x => x.HandleNameResolutionError(It.IsAny<Status>()), Times.Never);
        }

        private static Mock<IGrpcChannel> CreateGrpcChannelMock(MockBehavior mockBehavior = MockBehavior.Strict)
        {
            var channelMock = new Mock<IGrpcChannel>(mockBehavior);
            channelMock.Setup(x => x.LoggerFactory).Returns(NullLoggerFactory.Instance);
            channelMock.Setup(x => x.IsShutdown).Returns(false);
            channelMock.Setup(x => x.SyncContext).Returns(new GrpcSynchronizationContext((ex) => { throw ex; }));
            return channelMock;
        }

        private static GrpcNameResolutionResult CreateSampleGrpcNameResolutionResult()
        {
            var hostsAddresses = new List<GrpcHostAddress>() { new GrpcHostAddress("10.1.5.210"), new GrpcHostAddress("10.1.5.211") };
            var configOrError = GrpcServiceConfigOrError.FromConfig(new object());
            var attributes = GrpcAttributes.Empty;
            return new GrpcNameResolutionResult(hostsAddresses, configOrError, attributes);
        }
    }
}
