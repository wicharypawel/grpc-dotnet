using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    internal sealed class WeightedRandomPicker : ISubchannelPicker
    {
        internal readonly IReadOnlyList<WeightedChildPicker> _weightedPickers;
        private readonly Random _random;
        private readonly int _totalWeight;

        public WeightedRandomPicker(List<WeightedChildPicker> weightedChildPickers)
            : this(weightedChildPickers, new Random(Guid.NewGuid().GetHashCode()))
        {
        }

        public WeightedRandomPicker(List<WeightedChildPicker> weightedChildPickers, Random random)
        {
            if(weightedChildPickers == null)
            {
                throw new ArgumentNullException(nameof(weightedChildPickers));
            }
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }
            if (weightedChildPickers.Count == 0)
            {
                throw new ArgumentException($"{nameof(weightedChildPickers)} is empty");
            }
            _weightedPickers = weightedChildPickers;
            _random = random;
            _totalWeight = weightedChildPickers.Sum(x => x.Weight);
        }

        public GrpcSubChannel PickSubchannel()
        {
            if (_totalWeight == 0)
            {
                return _weightedPickers[_random.Next(_weightedPickers.Count)].ChildPicker.PickSubchannel();
            }
            var randomWeight = _random.Next(_totalWeight);
            var accumulatedWeight = 0;
            for (var i = 0; i < _weightedPickers.Count; i++)
            {
                accumulatedWeight += _weightedPickers[i].Weight;
                if (randomWeight < accumulatedWeight)
                {
                    return _weightedPickers[i].ChildPicker.PickSubchannel();
                }
            }
            throw new InvalidOperationException("ChildPicker not found");
        }

        internal sealed class WeightedChildPicker
        {
            public int Weight { get; }
            public ISubchannelPicker ChildPicker { get; }

            public WeightedChildPicker(int weight, ISubchannelPicker childPicker)
            {
                if (!(weight >= 0))
                {
                    throw new ArgumentException($"{nameof(weight)} can not be negative value");
                }
                if (childPicker == null)
                {
                    throw new ArgumentNullException(nameof(childPicker));
                }
                Weight = weight;
                ChildPicker = childPicker;
            }
        }
    }

    internal sealed class RoundRobinPicker : ISubchannelPicker
    {
        private int _subChannelsSelectionCounter = -1;
        internal IReadOnlyList<GrpcSubChannel> SubChannels { get; set; } = Array.Empty<GrpcSubChannel>();

        public RoundRobinPicker(List<GrpcSubChannel> subChannels)
        {
            SubChannels = subChannels;
        }

        public GrpcSubChannel PickSubchannel()
        {
            return SubChannels[Interlocked.Increment(ref _subChannelsSelectionCounter) % SubChannels.Count];
        }
    }

    internal sealed class EmptyPicker : ISubchannelPicker
    {
        public GrpcSubChannel PickSubchannel()
        {
            throw new InvalidOperationException("SubChannel not found");
        }
    }

    // TODO make this interface used by all policies
    // TODO this interface should be public
    // based on: https://github.com/grpc/grpc-java/blob/master/api/src/main/java/io/grpc/LoadBalancer.java
    internal interface ISubchannelPicker
    {
        public abstract GrpcSubChannel PickSubchannel();
    }
}
