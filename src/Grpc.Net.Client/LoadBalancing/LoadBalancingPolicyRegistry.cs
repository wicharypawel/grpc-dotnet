using Grpc.Net.Client.LoadBalancing.Internal;
using System;
using System.Collections.Concurrent;

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// Registry of <seealso cref="ILoadBalancingPolicyProvider"/>s. 
    /// </summary>
    public sealed class LoadBalancingPolicyRegistry
    {
        private readonly ConcurrentDictionary<string, ILoadBalancingPolicyProvider> _providers = new ConcurrentDictionary<string, ILoadBalancingPolicyProvider>();

        private LoadBalancingPolicyRegistry()
        {
        }

        private static LoadBalancingPolicyRegistry? Instance;
        private static readonly object LockObject = new object();

        /// <summary>
        /// Register provider.
        /// </summary>
        public void Register(ILoadBalancingPolicyProvider provider)
        {
            if (!_providers.TryAdd(provider.PolicyName, provider))
            {
                throw new InvalidOperationException("Deregistering load balancing policy provider failed");
            }
        }

        /// <summary>
        /// Deregisters provider.
        /// </summary>
        public void Deregister(ILoadBalancingPolicyProvider provider)
        {
            if(!_providers.TryRemove(provider.PolicyName, out _))
            {
                throw new InvalidOperationException("Deregistering load balancing policy provider failed");
            }
        }

        /// <summary>
        /// Returns the provider for the given load-balancing policy 
        /// </summary>
        /// <param name="policyName">Policy name written in snake_case eg. pick_first, round_robin, xds etc.</param>
        /// <returns>Load balancing policy or null if no suitable provider can be found</returns>
        public ILoadBalancingPolicyProvider? GetProvider(string policyName)
        {
            if(_providers.TryGetValue(policyName, out var loadBalancingPolicyProvider))
            {
                return loadBalancingPolicyProvider;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the default registry.
        /// </summary>
        /// <returns>Instance of <seealso cref="LoadBalancingPolicyRegistry"/>.</returns>
        public static LoadBalancingPolicyRegistry GetDefaultRegistry()
        {
            lock (LockObject)
            {
                if (Instance == null)
                {
                    Instance = new LoadBalancingPolicyRegistry();
                    Instance.Register(new PickFirstPolicyProvider());
                    Instance.Register(new RoundRobinPolicyProvider());
                }
                return Instance;
            }
        }
    }
}
