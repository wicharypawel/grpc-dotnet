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

            // Act
            var attributes = GrpcAttributes.Empty;

            // Assert
            Assert.Equal(0, attributes.GetKeysCount());
            Assert.Null(attributes.Get(sampleKey));
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
