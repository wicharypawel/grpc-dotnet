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

using System;
using System.Linq;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Core
{
    public sealed class GrpcAttributesTests
    {
        [Fact]
        public void ForBuildAttributes_UsingGrpcAttributes_VerifyContainValue()
        {
            // Arrange
            var sampleKey = GrpcAttributes.Key<string>.Create("test-only-key");
            var sampleValue = $"Sample-{Guid.NewGuid().ToString().Substring(0, 6)}";

            // Act
            var attributes = GrpcAttributes.Builder.NewBuilder().Add(sampleKey, sampleValue).Build();

            // Assert
            Assert.Equal(sampleValue, attributes.Get(sampleKey));
            Assert.Equal(1, attributes.GetKeysCount());
        }

        [Fact]
        public void ForBuildAttributesForDifferentTypes_UsingGrpcAttributes_VerifyContainValue()
        {
            // Arrange
            var sampleKey1 = GrpcAttributes.Key<int>.Create("test-only-key");
            var sampleValue1 = 5;
            var sampleKey2 = GrpcAttributes.Key<bool>.Create("test-only-key");
            var sampleValue2 = true;
            var sampleKey3 = GrpcAttributes.Key<double>.Create("test-only-key");
            var sampleValue3 = 1.2;
            var sampleKey4 = GrpcAttributes.Key<char>.Create("test-only-key");
            var sampleValue4 = 'f';
            var sampleKey5 = GrpcAttributes.Key<decimal>.Create("test-only-key");
            var sampleValue5 =  123.1233123542423M;
            var sampleKey6 = GrpcAttributes.Key<byte>.Create("test-only-key");
            byte sampleValue6 = 0x20;
            var sampleKey7 = GrpcAttributes.Key<ulong>.Create("test-only-key");
            ulong sampleValue7 = 123012;
            var sampleKey8 = GrpcAttributes.Key<int[]>.Create("test-only-key");
            var sampleValue8 = new int[] { 1, 2, 3 };
            var sampleKey9 = GrpcAttributes.Key<object>.Create("test-only-key");
            var sampleValue9 = new object();

            // Act
            var attributes = GrpcAttributes.Builder.NewBuilder()
                .Add(sampleKey1, sampleValue1)
                .Add(sampleKey2, sampleValue2)
                .Add(sampleKey3, sampleValue3)
                .Add(sampleKey4, sampleValue4)
                .Add(sampleKey5, sampleValue5)
                .Add(sampleKey6, sampleValue6)
                .Add(sampleKey7, sampleValue7)
                .Add(sampleKey8, sampleValue8)
                .Add(sampleKey9, sampleValue9)
                .Build();

            // Assert
            Assert.Equal(sampleValue1, attributes.GetValue(sampleKey1));
            Assert.Equal(sampleValue2, attributes.GetValue(sampleKey2));
            Assert.Equal(sampleValue3, attributes.GetValue(sampleKey3));
            Assert.Equal(sampleValue4, attributes.GetValue(sampleKey4));
            Assert.Equal(sampleValue5, attributes.GetValue(sampleKey5));
            Assert.Equal(sampleValue6, attributes.GetValue(sampleKey6));
            Assert.Equal(sampleValue7, attributes.GetValue(sampleKey7));
            Assert.True(Enumerable.SequenceEqual(sampleValue8, attributes.Get(sampleKey8)));
            Assert.Equal(sampleValue9, attributes.Get(sampleKey9));
        }

        [Fact]
        public void ForDuplicates_UsingGrpcAttributes_VerifyDuplicateOverwrite()
        {
            // Arrange
            var sampleKey = GrpcAttributes.Key<string>.Create("test-only-key");
            var sampleValue = $"Sample-{Guid.NewGuid().ToString().Substring(0, 6)}";

            // Act
            var attributes = GrpcAttributes.Builder.NewBuilder()
                .Add(sampleKey, "Value")
                .Add(sampleKey, sampleValue)
                .Add(GrpcAttributes.Key<string>.Create("test-only-key"), "This is not a duplicate because it has different key.")
                .Build();

            // Assert
            Assert.Equal(sampleValue, attributes.Get(sampleKey));
            Assert.Equal(2, attributes.GetKeysCount());
        }

        [Fact]
        public void ForAttributesToBuilder_UsingGrpcAttributes_VerifyContainPreviousValues()
        {
            // Arrange
            var sampleKey = GrpcAttributes.Key<string>.Create("test-only-key");
            var baseAttributes = GrpcAttributes.Builder.NewBuilder()
                .Add(sampleKey, $"Sample-{Guid.NewGuid().ToString().Substring(0, 6)}")
                .Add(GrpcAttributes.Key<string>.Create("test-only-key"), $"Sample-{Guid.NewGuid().ToString().Substring(0, 6)}")
                .Add(GrpcAttributes.Key<string>.Create("test-only-key"), $"Sample-{Guid.NewGuid().ToString().Substring(0, 6)}")
                .Build();

            // Act
            var attributes = GrpcAttributes.Builder.NewBuilder().Add(baseAttributes).Build();

            // Assert
            Assert.Equal(baseAttributes.GetKeysCount(), attributes.GetKeysCount());
            Assert.Equal(baseAttributes.Get(sampleKey), attributes.Get(sampleKey));
        }

        [Fact]
        public void ForEmpty_UsingGrpcAttributes_VerifyIndeedEmpty()
        {
            // Arrange
            var sampleKey = GrpcAttributes.Key<string>.Create("test-only-key");
            var sampleKey2 = GrpcAttributes.Key<int>.Create("test-only-numeric-key");

            // Act
            var attributes = GrpcAttributes.Empty;

            // Assert
            Assert.Equal(0, attributes.GetKeysCount());
            Assert.Null(attributes.Get(sampleKey));
            Assert.Null(attributes.GetValue(sampleKey2));
        }

        [Fact]
        public void ForAttributesToBuilderAndRemoveKey_UsingGrpcAttributes_VerifyContainPreviousValuesWithoutRemoved()
        {
            // Arrange
            var sampleKey = GrpcAttributes.Key<string>.Create("test-only-key");
            var baseAttributes = GrpcAttributes.Builder.NewBuilder()
                .Add(sampleKey, $"Sample-{Guid.NewGuid().ToString().Substring(0, 6)}")
                .Add(GrpcAttributes.Key<string>.Create("test-only-key"), $"Sample-{Guid.NewGuid().ToString().Substring(0, 6)}")
                .Add(GrpcAttributes.Key<string>.Create("test-only-key"), $"Sample-{Guid.NewGuid().ToString().Substring(0, 6)}")
                .Build();

            // Act
            var attributes = GrpcAttributes.Builder.NewBuilder().Add(baseAttributes).Remove(sampleKey).Build();

            // Assert
            Assert.Equal(baseAttributes.GetKeysCount() - 1, attributes.GetKeysCount());
            Assert.Null(attributes.Get(sampleKey));
        }

        [Fact]
        public void ForModifiedBuilder_UsingGrpcAttributes_VerifyChangingBuilderNotInfluenceCreatedAttributes()
        {
            // Arrange
            var sampleKey = GrpcAttributes.Key<string>.Create("test-only-key");
            var builder = GrpcAttributes.Builder.NewBuilder()
                .Add(sampleKey, $"Sample-{Guid.NewGuid().ToString().Substring(0, 6)}")
                .Add(GrpcAttributes.Key<string>.Create("test-only-key"), $"Sample-{Guid.NewGuid().ToString().Substring(0, 6)}")
                .Add(GrpcAttributes.Key<string>.Create("test-only-key"), $"Sample-{Guid.NewGuid().ToString().Substring(0, 6)}");

            // Act
            var attributes = builder.Build();
            builder.Remove(sampleKey);

            // Assert
            Assert.Equal(3, attributes.GetKeysCount());
            Assert.NotNull(attributes.Get(sampleKey));
        }
    }
}
