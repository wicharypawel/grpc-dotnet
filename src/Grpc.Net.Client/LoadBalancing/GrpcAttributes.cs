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
using System.Collections.Generic;

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// An immutable type-safe container of attributes.
    /// </summary>
    public sealed class GrpcAttributes
    {
        /// <summary>
        /// An instance of attributes with no data.
        /// </summary>
        public static readonly GrpcAttributes Empty = new GrpcAttributes(new Dictionary<IKey, object>());

        private readonly IReadOnlyDictionary<IKey, object> _data;

        private GrpcAttributes(Dictionary<IKey, object> data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        /// <summary>
        /// Gets the value for the key, or null if it's not present.
        /// </summary>
        /// <typeparam name="TValue">Type of the value.</typeparam>
        /// <param name="key">Key for metadata.</param>
        /// <returns>Metadata value or null.</returns>
        public TValue? Get<TValue>(Key<TValue> key) where TValue : class
        {
            if (_data.TryGetValue(key, out var result))
            {
                return result as TValue;
            }
            return null; 
        }

        private interface IKey
        {
        }

        /// <summary>
        /// Key for an key-value pair.
        /// </summary>
        /// <typeparam name="TValue">Type of the value.</typeparam>
        public sealed class Key<TValue> : IKey where TValue : class
        {
            private readonly string _debugString;

            private Key(string debugString)
            {
                _debugString = debugString ?? throw new ArgumentNullException(nameof(debugString));
            }

            /// <summary>
            /// Returns a string that represents the current object.
            /// </summary>
            /// <returns>A string that represents the current object.</returns>
            public override string ToString()
            {
                return _debugString;
            }

            /// <summary>
            /// Factory method for creating instances of <see cref="Key{TValue}"/>.
            /// </summary>
            /// <param name="debugString">A string used to describe the key, used for debugging.</param>
            /// <returns>A new key instance.</returns>
            public static Key<TValue> Create(string debugString)
            {
                return new Key<TValue>(debugString);
            }
        }

        /// <summary>
        /// The helper class to build an <see cref="GrpcAttributes"/> instance.
        /// </summary>
        public sealed class Builder
        {
            private readonly Dictionary<IKey, object> _data;

            private Builder()
            {
                _data = new Dictionary<IKey, object>();
            }

            /// <summary>
            /// Sets a new key-value pair. New values for the same key overwrite previous values.
            /// </summary>
            /// <typeparam name="TValue">Type of the value.</typeparam>
            /// <param name="key">Key for an key-value pair.</param>
            /// <param name="value">Value for an key-value pair.</param>
            /// <returns>This builder instance.</returns>
            public Builder Add<TValue>(Key<TValue> key, TValue value) where TValue : class
            {
                _data[key] = value;
                return this;
            }

            /// <summary>
            /// Copy all key-value pairs from already existing <see cref="GrpcAttributes"/> instance. 
            /// New values for the same key overwrite previous values.
            /// </summary>
            /// <param name="other">Source of key-value pairs.</param>
            /// <returns>This builder instance.</returns>
            public Builder Add(GrpcAttributes other)
            {
                foreach (var key in other._data.Keys)
                {
                    _data[key] = other._data[key];
                }
                return this;
            }

            /// <summary>
            /// Removes a new key-value pair if exist.
            /// </summary>
            /// <typeparam name="TValue">Type of the value.</typeparam>
            /// <param name="key">Key for an key-value pair.</param>
            /// <returns>This builder instance.</returns>
            public Builder Remove<TValue>(Key<TValue> key) where TValue : class
            {
                if (_data.ContainsKey(key))
                {
                    _data.Remove(key);
                }
                return this;
            }

            /// <summary>
            /// Creates a new instance of <seealso cref="GrpcAttributes"/>. 
            /// </summary>
            /// <returns>New attributes instance.</returns>
            public GrpcAttributes Build()
            {
                return new GrpcAttributes(new Dictionary<IKey, object>(_data));
            }

            /// <summary>
            /// Creates a new instance of <seealso cref="Builder"/>.
            /// </summary>
            /// <returns>New builder instance.</returns>
            public static Builder NewBuilder()
            {
                return new Builder();
            }
        }
    }
}
