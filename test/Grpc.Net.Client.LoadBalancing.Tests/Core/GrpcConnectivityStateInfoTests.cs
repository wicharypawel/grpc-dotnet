using Grpc.Core;
using System;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Core
{
    public sealed class GrpcConnectivityStateInfoTests
    {
        [Fact]
        public void ForNonErrorIdle_UseGrpcConnectivityStateInfo_ReturnOkIdle()
        {
            // Arrange
            // Act
            var info = GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.IDLE);

            // Assert
            Assert.Equal(GrpcConnectivityState.IDLE, info.State);
            Assert.Equal(StatusCode.OK, info.Status.StatusCode);
        }

        [Fact]
        public void ForNonErrorInvalid_UseGrpcConnectivityStateInfo_Throw()
        {
            // Arrange
            // Act
            // Assert
            Assert.Throws<ArgumentException>(() =>
            {
                var _ = GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.TRANSIENT_FAILURE);
            });
        }

        [Fact]
        public void ForTransientFailure_UseGrpcConnectivityStateInfo_ReturnUnavailableTransientError()
        {
            // Arrange
            // Act
            var info = GrpcConnectivityStateInfo.ForTransientFailure(new Status(StatusCode.Unavailable, string.Empty));

            // Assert
            Assert.Equal(GrpcConnectivityState.TRANSIENT_FAILURE, info.State);
            Assert.Equal(StatusCode.Unavailable, info.Status.StatusCode);
        }

        [Fact]
        public void ForTransientFailureInvalid_UseGrpcConnectivityStateInfo_Throw()
        {
            // Arrange
            // Act
            // Assert
            Assert.Throws<ArgumentException>(() =>
            {
                var _ = GrpcConnectivityStateInfo.ForTransientFailure(Status.DefaultSuccess);
            });
        }
    }
}
