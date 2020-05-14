using Grpc.Net.Client.Internal;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Core
{
    public sealed class InterlockedBoolTests
    {
        [Fact]
        public void ForVerifyInitialization_UsingInterlockedBool()
        {
            // Arrange
            // Act
            // Assert
            Assert.False(new InterlockedBool().Get());
            Assert.True(new InterlockedBool(true).Get());
            Assert.False(new InterlockedBool(false).Get());
        }

        [Fact]
        public void ForVerifySetValue_UsingInterlockedBool()
        {
            // Arrange
            var value = new InterlockedBool(false);
            
            // Act
            value.Set(true);
            Assert.True(value.Get());
            value.Set(false);

            // Assert
            Assert.False(value.Get());
        }

        [Fact]
        public void ForVerifyGetAndSet_UsingInterlockedBool()
        {
            // Arrange
            var value = new InterlockedBool(false);

            // Act
            var previusValue = value.GetAndSet(true);

            // Assert
            Assert.False(previusValue);
            Assert.True(value.Get());
        }

        [Theory]
        [InlineData(false, true, true, true)]
        [InlineData(true, false, false, false)]
        [InlineData(false, false, true, false)]
        [InlineData(true, true, false, false)]
        public void ForVerifyCompareAndSet_UsingInterlockedBool(bool expect, bool update, bool changed, bool final)
        {
            // Arrange
            var value = new InterlockedBool(false);

            // Act
            var changeSuccess = value.CompareAndSet(expect, update);

            // Assert
            Assert.Equal(changed, changeSuccess);
            Assert.Equal(final, value.Get());
        }
    }
}
