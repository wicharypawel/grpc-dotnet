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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Core
{
    public sealed class GrpcHelperTests
    {
        [Fact]
        public void ForCreateSubChannel_UseGrpcHelper_VerifySubChannelType()
        {
            // Arrange
            var syncContext = new GrpcSynchronizationContext((ex) => throw ex);
            var channelMock = new Mock<IGrpcChannel>(MockBehavior.Strict);
            channelMock.Setup(x => x.LoggerFactory).Returns(NullLoggerFactory.Instance);
            channelMock.Setup(x => x.SyncContext).Returns(syncContext);
            var helper = new GrpcHelper(channelMock.Object);
            var arguments = new CreateSubchannelArgs(new UriBuilder("http://102.1.1.5:80").Uri, GrpcAttributes.Empty);
            IGrpcSubChannel? subchannel = null;

            // Act
            syncContext.Execute(() =>
            {
                subchannel = helper.CreateSubChannel(arguments);
            });

            // Assert
            Assert.NotNull(subchannel);
            Assert.Equal(typeof(GrpcSubChannel), subchannel?.GetType());
        }

        [Fact]
        public void ForCreateSubChannelOutSideOfSyncContext_UseGrpcHelper_Throw()
        {
            // Arrange
            var syncContext = new GrpcSynchronizationContext((ex) => throw ex);
            var channelMock = new Mock<IGrpcChannel>(MockBehavior.Strict);
            channelMock.Setup(x => x.LoggerFactory).Returns(NullLoggerFactory.Instance);
            channelMock.Setup(x => x.SyncContext).Returns(syncContext);
            var helper = new GrpcHelper(channelMock.Object);
            var arguments = new CreateSubchannelArgs(new UriBuilder("http://102.1.1.5:80").Uri, GrpcAttributes.Empty);

            // Act
            // Assert
            var error = Assert.Throws<InvalidOperationException>(() => 
            {
                helper.CreateSubChannel(arguments);
            });
            Assert.Equal("Not called from the SynchronizationContext", error.Message);
        }

        [Fact]
        public void ForUpdateBalancingState_UseGrpcHelper_VerifyStatusAndPickerChange()
        {
            // Arrange
            var channelStateManager = new GrpcConnectivityStateManager();
            var syncContext = new GrpcSynchronizationContext((ex) => throw ex);
            var channelMock = new Mock<IGrpcChannel>(MockBehavior.Strict);
            channelMock.Setup(x => x.LoggerFactory).Returns(NullLoggerFactory.Instance);
            channelMock.Setup(x => x.SyncContext).Returns(syncContext);
            channelMock.Setup(x => x.UpdateSubchannelPicker(It.IsAny<IGrpcSubChannelPicker>()));
            channelMock.Setup(x => x.ChannelStateManager).Returns(channelStateManager);
            channelMock.Setup(x => x.IsShutdown).Returns(false);
            var helper = new GrpcHelper(channelMock.Object);
            var newState = GrpcConnectivityState.CONNECTING;
            var newPicker = new EmptyPicker();

            // Act
            syncContext.Execute(() =>
            {
                helper.UpdateBalancingState(newState, newPicker);
            });

            // Assert
            channelMock.Verify(x => x.UpdateSubchannelPicker(newPicker), Times.Once);
            Assert.Equal(newState, channelStateManager.GetState());
        }

        [Fact]
        public void ForUpdateBalancingStateAndShutdownChannel_UseGrpcHelper_VerifySkipChange()
        {
            // Arrange
            var channelStateManager = new GrpcConnectivityStateManager();
            var initialState = channelStateManager.GetState();
            var syncContext = new GrpcSynchronizationContext((ex) => throw ex);
            var channelMock = new Mock<IGrpcChannel>(MockBehavior.Strict);
            channelMock.Setup(x => x.LoggerFactory).Returns(NullLoggerFactory.Instance);
            channelMock.Setup(x => x.SyncContext).Returns(syncContext);
            channelMock.Setup(x => x.UpdateSubchannelPicker(It.IsAny<IGrpcSubChannelPicker>()));
            channelMock.Setup(x => x.ChannelStateManager).Returns(channelStateManager);
            channelMock.Setup(x => x.IsShutdown).Returns(true);
            var helper = new GrpcHelper(channelMock.Object);
            var newState = GrpcConnectivityState.CONNECTING;
            var newPicker = new EmptyPicker();

            // Act
            syncContext.Execute(() =>
            {
                helper.UpdateBalancingState(newState, newPicker);
            });

            // Assert
            channelMock.Verify(x => x.UpdateSubchannelPicker(newPicker), Times.Never);
            Assert.Equal(initialState, channelStateManager.GetState());
        }

        [Fact]
        public void ForUpdateBalancingStateWithShutdownStatus_UseGrpcHelper_IgnoreSettingStatusToShutdown()
        {
            // Arrange
            var channelStateManager = new GrpcConnectivityStateManager();
            var initialState = channelStateManager.GetState();
            var syncContext = new GrpcSynchronizationContext((ex) => throw ex);
            var channelMock = new Mock<IGrpcChannel>(MockBehavior.Strict);
            channelMock.Setup(x => x.LoggerFactory).Returns(NullLoggerFactory.Instance);
            channelMock.Setup(x => x.SyncContext).Returns(syncContext);
            channelMock.Setup(x => x.UpdateSubchannelPicker(It.IsAny<IGrpcSubChannelPicker>()));
            channelMock.Setup(x => x.ChannelStateManager).Returns(channelStateManager);
            channelMock.Setup(x => x.IsShutdown).Returns(false);
            var helper = new GrpcHelper(channelMock.Object);
            var newState = GrpcConnectivityState.SHUTDOWN; // lb can not shutdown the channel
            var newPicker = new EmptyPicker();

            // Act
            syncContext.Execute(() =>
            {
                helper.UpdateBalancingState(newState, newPicker);
            });

            // Assert
            channelMock.Verify(x => x.UpdateSubchannelPicker(newPicker), Times.Once);
            Assert.Equal(initialState, channelStateManager.GetState());
        }

        [Fact]
        public void ForUpdateBalancingStateOutSideOfSyncContext_UseGrpcHelper_Throw()
        {
            // Arrange
            var channelStateManager = new GrpcConnectivityStateManager();
            var syncContext = new GrpcSynchronizationContext((ex) => throw ex);
            var channelMock = new Mock<IGrpcChannel>(MockBehavior.Strict);
            channelMock.Setup(x => x.LoggerFactory).Returns(NullLoggerFactory.Instance);
            channelMock.Setup(x => x.SyncContext).Returns(syncContext);
            var helper = new GrpcHelper(channelMock.Object);

            // Act
            // Assert
            var error = Assert.Throws<InvalidOperationException>(() =>
            {
                helper.UpdateBalancingState(GrpcConnectivityState.CONNECTING, new EmptyPicker());
            });
            Assert.Equal("Not called from the SynchronizationContext", error.Message);
        }

        [Fact]
        public void ForRefreshNameResolution_UseGrpcHelper_RefreshCalledOnChannel()
        {
            // Arrange
            var syncContext = new GrpcSynchronizationContext((ex) => throw ex);
            var channelMock = new Mock<IGrpcChannel>(MockBehavior.Strict);
            channelMock.Setup(x => x.LoggerFactory).Returns(NullLoggerFactory.Instance);
            channelMock.Setup(x => x.SyncContext).Returns(syncContext);
            channelMock.Setup(x => x.RefreshAndResetNameResolution());
            channelMock.Setup(x => x.IsShutdown).Returns(false);
            var helper = new GrpcHelper(channelMock.Object);

            // Act
            syncContext.Execute(() =>
            {
                helper.RefreshNameResolution();
            });

            // Assert
            channelMock.Verify(x => x.RefreshAndResetNameResolution(), Times.Once);
        }

        [Fact]
        public void ForRefreshNameResolutionAndShutdownChannel_UseGrpcHelper_VerifySkipRefresh()
        {
            // Arrange
            var syncContext = new GrpcSynchronizationContext((ex) => throw ex);
            var channelMock = new Mock<IGrpcChannel>(MockBehavior.Strict);
            channelMock.Setup(x => x.LoggerFactory).Returns(NullLoggerFactory.Instance);
            channelMock.Setup(x => x.SyncContext).Returns(syncContext);
            channelMock.Setup(x => x.RefreshAndResetNameResolution());
            channelMock.Setup(x => x.IsShutdown).Returns(true);
            var helper = new GrpcHelper(channelMock.Object);

            // Act
            syncContext.Execute(() =>
            {
                helper.RefreshNameResolution();
            });

            // Assert
            channelMock.Verify(x => x.RefreshAndResetNameResolution(), Times.Never);
        }

        [Fact]
        public void ForRefreshNameResolutionOutSideOfSyncContext_UseGrpcHelper_Throw()
        {
            // Arrange
            var syncContext = new GrpcSynchronizationContext((ex) => throw ex);
            var channelMock = new Mock<IGrpcChannel>(MockBehavior.Strict);
            channelMock.Setup(x => x.LoggerFactory).Returns(NullLoggerFactory.Instance);
            channelMock.Setup(x => x.SyncContext).Returns(syncContext);
            var helper = new GrpcHelper(channelMock.Object);

            // Act
            // Assert
            var error = Assert.Throws<InvalidOperationException>(() =>
            {
                helper.RefreshNameResolution();
            });
            Assert.Equal("Not called from the SynchronizationContext", error.Message);
        }

        [Fact]
        public void ForGetSynchronizationContext_UseGrpcHelper_VerifyValue()
        {
            // Arrange
            var syncContext = new GrpcSynchronizationContext((ex) => throw ex);
            var channelMock = new Mock<IGrpcChannel>(MockBehavior.Strict);
            channelMock.Setup(x => x.LoggerFactory).Returns(NullLoggerFactory.Instance);
            channelMock.Setup(x => x.SyncContext).Returns(syncContext);
            var helper = new GrpcHelper(channelMock.Object);
            GrpcSynchronizationContext? returnedContext = null;

            // Act
            returnedContext = helper.GetSynchronizationContext();

            // Assert
            Assert.NotNull(returnedContext);
            Assert.Equal(syncContext, returnedContext);
        }

        [Fact]
        public void ForGetAuthority_UseGrpcHelper_VerifyValue()
        {
            // Arrange
            var syncContext = new GrpcSynchronizationContext((ex) => throw ex);
            var channelMock = new Mock<IGrpcChannel>(MockBehavior.Strict);
            channelMock.Setup(x => x.LoggerFactory).Returns(NullLoggerFactory.Instance);
            channelMock.Setup(x => x.SyncContext).Returns(syncContext);
            channelMock.Setup(x => x.Address).Returns(new UriBuilder("http://google.apis.com").Uri);
            var helper = new GrpcHelper(channelMock.Object);
            string? returnedAuthority = null;

            // Act
            returnedAuthority = helper.GetAuthority();

            // Assert
            Assert.NotNull(returnedAuthority);
            Assert.Equal("google.apis.com:80", returnedAuthority);
        }

        [Fact]
        public void ForGetAddress_UseGrpcHelper_VerifyValue()
        {
            // Arrange
            var syncContext = new GrpcSynchronizationContext((ex) => throw ex);
            var channelMock = new Mock<IGrpcChannel>(MockBehavior.Strict);
            channelMock.Setup(x => x.LoggerFactory).Returns(NullLoggerFactory.Instance);
            channelMock.Setup(x => x.SyncContext).Returns(syncContext);
            channelMock.Setup(x => x.Address).Returns(new UriBuilder("http://google.apis.com").Uri);
            var helper = new GrpcHelper(channelMock.Object);
            Uri? returnedAddress = null;

            // Act
            returnedAddress = helper.GetAddress();

            // Assert
            Assert.NotNull(returnedAddress);
            Assert.Equal(new UriBuilder("http://google.apis.com").Uri, returnedAddress);
        }

        [Fact]
        public void ForAddressAndAlwaysIncludePort_UseGrpcHelperGetAuthorityCore_VerifyReturnHostNameAndPort()
        {
            // Arrange
            // Act
            // Assert
            Assert.Equal("google.apis.com:80", GrpcHelper.GetAuthorityCore(new UriBuilder("http://google.apis.com").Uri, true));
            Assert.Equal("google.apis.com:80", GrpcHelper.GetAuthorityCore(new UriBuilder("http://google.apis.com:80").Uri, true));
            Assert.Equal("google.apis.com:8080", GrpcHelper.GetAuthorityCore(new UriBuilder("http://google.apis.com:8080").Uri, true));
            Assert.Equal("google.apis.com:443", GrpcHelper.GetAuthorityCore(new UriBuilder("http://google.apis.com:443").Uri, true));
            Assert.Equal("google.apis.com:443", GrpcHelper.GetAuthorityCore(new UriBuilder("https://google.apis.com").Uri, true));
            Assert.Equal("google.apis.com:443", GrpcHelper.GetAuthorityCore(new UriBuilder("https://google.apis.com:443").Uri, true));
            Assert.Equal("google.apis.com:8080", GrpcHelper.GetAuthorityCore(new UriBuilder("https://google.apis.com:8080").Uri, true));
            Assert.Equal("google.apis.com:80", GrpcHelper.GetAuthorityCore(new UriBuilder("dns://google.apis.com").Uri, true));
            Assert.Equal("google.apis.com:80", GrpcHelper.GetAuthorityCore(new UriBuilder("dns://google.apis.com:80").Uri, true));
            Assert.Equal("google.apis.com:443", GrpcHelper.GetAuthorityCore(new UriBuilder("dns://google.apis.com:443").Uri, true));
            Assert.Equal("google.apis.com:9000", GrpcHelper.GetAuthorityCore(new UriBuilder("dns://google.apis.com:9000").Uri, true));
            Assert.Equal("google.apis.com:80", GrpcHelper.GetAuthorityCore(new UriBuilder("xds://google.apis.com").Uri, true));
            Assert.Equal("google.apis.com:80", GrpcHelper.GetAuthorityCore(new UriBuilder("xds://google.apis.com:80").Uri, true));
            Assert.Equal("google.apis.com:443", GrpcHelper.GetAuthorityCore(new UriBuilder("xds://google.apis.com:443").Uri, true));
            Assert.Equal("google.apis.com:9000", GrpcHelper.GetAuthorityCore(new UriBuilder("xds://google.apis.com:9000").Uri, true));
        }
    }
}
