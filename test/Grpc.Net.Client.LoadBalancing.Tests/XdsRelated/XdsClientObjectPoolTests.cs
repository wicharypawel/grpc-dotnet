using Grpc.Net.Client.LoadBalancing.Extensions.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.XdsRelated
{
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
    public sealed class XdsClientObjectPoolTests
    {
        [Fact]
        public void ForNewPool_UseXdsClientObjectPool_ReturnInstanceOfXdsClient()
        {
            // Arrange
            var pool = new XdsClientObjectPool(NullLoggerFactory.Instance);
            var xdsClientMock = new Mock<IXdsClient>(MockBehavior.Strict);
            XdsClientFactory.OverrideXdsClient = xdsClientMock.Object; 

            // Act
            var xdsClient = pool.GetObject();

            // Assert
            Assert.NotNull(xdsClient);
            Assert.Equal(xdsClientMock.Object, xdsClient);
        }

        [Fact]
        public void ForTwoConsecutiveGetObject_UseXdsClientObjectPool_ReturnTheSameInstanceOfXdsClient()
        {
            // Arrange
            var pool = new XdsClientObjectPool(NullLoggerFactory.Instance);
            var xdsClientMock = new Mock<IXdsClient>(MockBehavior.Strict);
            XdsClientFactory.OverrideXdsClient = xdsClientMock.Object;

            // Act
            var xdsClient1 = pool.GetObject();
            XdsClientFactory.OverrideXdsClient = null; // ensure that factory is not called
            var xdsClient2 = pool.GetObject();
            var xdsClient3 = pool.GetObject();

            // Assert
            Assert.NotNull(xdsClient1);
            Assert.NotNull(xdsClient2);
            Assert.NotNull(xdsClient3);
            Assert.Equal(xdsClient1, xdsClient2);
            Assert.Equal(xdsClient2, xdsClient3);
        }

        [Fact]
        public void ForLastReference_UseXdsClientObjectPool_DisposeXdsClient()
        {
            // Arrange
            var pool = new XdsClientObjectPool(NullLoggerFactory.Instance);
            var xdsClientMock = new Mock<IXdsClient>(MockBehavior.Strict);
            xdsClientMock.Setup(x => x.Dispose()).Verifiable();
            XdsClientFactory.OverrideXdsClient = xdsClientMock.Object;

            // Act
            var xdsClient = pool.GetObject(); // increment reference 1
            var _ = pool.GetObject(); // increment reference 2
            var __ = pool.GetObject(); // increment reference 3
            pool.ReturnObject(xdsClient); // decrement reference 2
            xdsClientMock.Verify(x => x.Dispose(), Times.Never);
            pool.ReturnObject(xdsClient); // decrement reference 1
            xdsClientMock.Verify(x => x.Dispose(), Times.Never);
            pool.ReturnObject(xdsClient); // decrement reference 0

            // Assert
            xdsClientMock.Verify(x => x.Dispose(), Times.Once);
        }

        [Fact]
        public void ForDifferentObjectReturnedToPool_UseXdsClientObjectPool_ThrowError()
        {
            // Arrange
            var pool = new XdsClientObjectPool(NullLoggerFactory.Instance);
            var xdsClientMock = new Mock<IXdsClient>(MockBehavior.Strict);
            XdsClientFactory.OverrideXdsClient = xdsClientMock.Object;
            var xdsClientMock2 = new Mock<IXdsClient>(MockBehavior.Strict);

            // Act
            var xdsClient = pool.GetObject();

            // Assert
            var exception = Assert.Throws<InvalidOperationException>(() => {
                pool.ReturnObject(xdsClientMock2.Object);
            });
            Assert.Equal("the returned instance does not match current XdsClient", exception.Message);
            exception = Assert.Throws<InvalidOperationException>(() => {
                pool.ReturnObject(null);
            });
            Assert.Equal("the returned instance does not match current XdsClient", exception.Message);
        }

        [Fact]
        public void ForNullReturnedToEmptyPool_UseXdsClientObjectPool_ThrowError()
        {
            // Arrange
            var pool = new XdsClientObjectPool(NullLoggerFactory.Instance);
            var xdsClientMock = new Mock<IXdsClient>(MockBehavior.Strict);
            XdsClientFactory.OverrideXdsClient = xdsClientMock.Object;

            // Act
            // Assert
            var exception = Assert.Throws<InvalidOperationException>(() => {
                pool.ReturnObject(null);
            });
            Assert.Equal("referenceCount of XdsClient less than 0", exception.Message);
        }
    }
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
}
