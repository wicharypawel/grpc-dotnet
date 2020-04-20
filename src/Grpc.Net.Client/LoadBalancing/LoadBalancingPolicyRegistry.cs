using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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
            return GetDefaultRegistry(NullLoggerFactory.Instance);
        }

        /// <summary>
        /// Returns the default registry.
        /// </summary>
        /// <param name="loggerFactory">Logger factory instance.</param>
        /// <returns>Instance of <seealso cref="LoadBalancingPolicyRegistry"/>.</returns>
        public static LoadBalancingPolicyRegistry GetDefaultRegistry(ILoggerFactory loggerFactory)
        {
            if (Instance != null)
            {
                return Instance;
            }
            lock (LockObject)
            {
                if (Instance == null)
                {
                    var logger = loggerFactory.CreateLogger<LoadBalancingPolicyRegistry>();
                    Instance = new LoadBalancingPolicyRegistry();
                    logger.LogDebug($"{nameof(LoadBalancingPolicyRegistry)} created");
                    foreach (var provider in GetPolicyProvidersFromAppDomain())
                    {
                        Instance.Register(provider);
                        logger.LogDebug($"{nameof(LoadBalancingPolicyRegistry)} found {provider.GetType().Name}");
                    }
                }
                return Instance;
            }
        }

        private static ILoadBalancingPolicyProvider[] GetPolicyProvidersFromAppDomain()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic)
                .Where(assembly => assembly.FullName.StartsWith("Grpc.Net.Client", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return assemblies.SelectMany(GetPolicyProvidersFromAssembly).ToArray();
        }

        private static ILoadBalancingPolicyProvider[] GetPolicyProvidersFromAssembly(Assembly assembly)
        {
            return assembly.GetTypes()
                .Where(type => type.IsClass && !type.IsAbstract)
                .Where(type => typeof(ILoadBalancingPolicyProvider).IsAssignableFrom(type))
                .Select(type => Activator.CreateInstance(type))
                .Cast<ILoadBalancingPolicyProvider>()
                .ToArray();
        }
    }
}
