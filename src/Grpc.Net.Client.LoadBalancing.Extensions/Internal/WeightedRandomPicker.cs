using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    internal sealed class WeightedRandomPicker : IGrpcSubChannelPicker
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
                throw new ArgumentException($"{nameof(weightedChildPickers)} is empty.");
            }
            _weightedPickers = weightedChildPickers;
            _random = random;
            _totalWeight = weightedChildPickers.Sum(x => x.Weight);
        }

        public GrpcPickResult GetNextSubChannel()
        {
            if (_totalWeight == 0)
            {
                return _weightedPickers[_random.Next(_weightedPickers.Count)].ChildPicker.GetNextSubChannel();
            }
            var randomWeight = _random.Next(_totalWeight);
            var accumulatedWeight = 0;
            for (var i = 0; i < _weightedPickers.Count; i++)
            {
                accumulatedWeight += _weightedPickers[i].Weight;
                if (randomWeight < accumulatedWeight)
                {
                    return _weightedPickers[i].ChildPicker.GetNextSubChannel();
                }
            }
            throw new InvalidOperationException("ChildPicker not found.");
        }

        public void Dispose()
        {
        }

        internal sealed class WeightedChildPicker
        {
            public int Weight { get; }
            public IGrpcSubChannelPicker ChildPicker { get; }

            public WeightedChildPicker(int weight, IGrpcSubChannelPicker childPicker)
            {
                if (!(weight >= 0))
                {
                    throw new ArgumentException($"{nameof(weight)} can not be negative value.");
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

    internal sealed class RoundRobinPicker : IGrpcSubChannelPicker
    {
        private int _subChannelsSelectionCounter = -1;
        internal IReadOnlyList<IGrpcSubChannel> SubChannels { get; set; } = Array.Empty<IGrpcSubChannel>();
        internal IReadOnlyList<GrpcPickResult> PickResults { get; set; } = Array.Empty<GrpcPickResult>();

        public RoundRobinPicker(List<IGrpcSubChannel> subChannels)
        {
            SubChannels = subChannels;
            PickResults = subChannels.Select(x => GrpcPickResult.WithSubChannel(x)).ToArray();
        }

        public GrpcPickResult GetNextSubChannel()
        {
            return PickResults[Interlocked.Increment(ref _subChannelsSelectionCounter) % PickResults.Count];
        }

        public void Dispose()
        {
        }
    }

    internal sealed class EmptyPicker : IGrpcSubChannelPicker
    {
        public GrpcPickResult GetNextSubChannel()
        {
            return GrpcPickResult.WithNoResult();
        }

        public void Dispose()
        {
        }
    }
}
