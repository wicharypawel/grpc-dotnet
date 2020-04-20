﻿using System.Collections.Generic;

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// An immutable container of attributes.
    /// </summary>
    public sealed class GrpcAttributes
    {
        /// <summary>
        /// Instance of attributes with no data.
        /// </summary>
        public static readonly GrpcAttributes Empty = new GrpcAttributes(new Dictionary<string, object>());

        private readonly IReadOnlyDictionary<string, object> _data;

        /// <summary>
        /// Creates a new instance of <seealso cref="GrpcAttributes"/>. 
        /// </summary>
        /// <param name="data">Dictionary of metadata.</param>
        public GrpcAttributes(Dictionary<string, object> data)
        {
            _data = data;
        }

        /// <summary>
        /// Gets the value for the key, or null if it's not present.
        /// </summary>
        /// <param name="key">Key for metadata.</param>
        /// <returns>Metadata value or null.</returns>
        public object? Get(string key)
        {
            if (_data.TryGetValue(key, out var result))
            {
                return result;
            }
            return null; 
        }

        /// <summary>
        /// Create new instance of <see cref="GrpcAttributes"/> based on existing attributes.
        /// All data will be copied to a new instance and then new data will be added.
        /// </summary>
        /// <param name="key">New metadata key. In case of duplicated keys, new value will override original.</param>
        /// <param name="value">New metadata value. In case of duplicated keys, new value will override original.</param>
        /// <returns>Creates a new instance of <seealso cref="GrpcAttributes"/>.</returns>
        public GrpcAttributes Add(string key, object value)
        {
            var result = new Dictionary<string, object>(_data);
            result.Add(key, value);
            return new GrpcAttributes(result);
        }
    }
}
