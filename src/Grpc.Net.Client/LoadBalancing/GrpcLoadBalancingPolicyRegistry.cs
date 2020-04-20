using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// Registry of <seealso cref="IGrpcLoadBalancingPolicyProvider"/>s. 
    /// </summary>
    public sealed class GrpcLoadBalancingPolicyRegistry
    {
        private readonly ConcurrentDictionary<string, IGrpcLoadBalancingPolicyProvider> _providers = new ConcurrentDictionary<string, IGrpcLoadBalancingPolicyProvider>();

        private GrpcLoadBalancingPolicyRegistry()
        {
        }

        private static GrpcLoadBalancingPolicyRegistry? Instance;
        private static readonly object LockObject = new object();

        /// <summary>
        /// Register provider.
        /// </summary>
        public void Register(IGrpcLoadBalancingPolicyProvider provider)
        {
            if (!_providers.TryAdd(provider.PolicyName, provider))
            {
                throw new InvalidOperationException("Deregistering load balancing policy provider failed");
            }
        }

        /// <summary>
        /// Deregisters provider.
        /// </summary>
        public void Deregister(IGrpcLoadBalancingPolicyProvider provider)
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
        public IGrpcLoadBalancingPolicyProvider? GetProvider(string policyName)
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
        /// <returns>Instance of <seealso cref="GrpcLoadBalancingPolicyRegistry"/>.</returns>
        public static GrpcLoadBalancingPolicyRegistry GetDefaultRegistry()
        {
            return GetDefaultRegistry(NullLoggerFactory.Instance);
        }

        /// <summary>
        /// Returns the default registry.
        /// </summary>
        /// <param name="loggerFactory">Logger factory instance.</param>
        /// <returns>Instance of <seealso cref="GrpcLoadBalancingPolicyRegistry"/>.</returns>
        public static GrpcLoadBalancingPolicyRegistry GetDefaultRegistry(ILoggerFactory loggerFactory)
        {
            if (Instance != null)
            {
                return Instance;
            }
            lock (LockObject)
            {
                if (Instance == null)
                {
                    var logger = loggerFactory.CreateLogger<GrpcLoadBalancingPolicyRegistry>();
                    Instance = new GrpcLoadBalancingPolicyRegistry();
                    logger.LogDebug($"{nameof(GrpcLoadBalancingPolicyRegistry)} created");
                    foreach (var provider in GetPolicyProvidersFromAppDomain())
                    {
                        Instance.Register(provider);
                        logger.LogDebug($"{nameof(GrpcLoadBalancingPolicyRegistry)} found {provider.GetType().Name}");
                    }
                }
                return Instance;
            }
        }

        private static IGrpcLoadBalancingPolicyProvider[] GetPolicyProvidersFromAppDomain()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic)
                .Where(assembly => assembly.FullName.StartsWith("Grpc.Net.Client", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return assemblies.SelectMany(GetPolicyProvidersFromAssembly).ToArray();
        }

        private static IGrpcLoadBalancingPolicyProvider[] GetPolicyProvidersFromAssembly(Assembly assembly)
        {
            return assembly.GetTypes()
                .Where(type => type.IsClass && !type.IsAbstract)
                .Where(type => typeof(IGrpcLoadBalancingPolicyProvider).IsAssignableFrom(type))
                .Select(type => Activator.CreateInstance(type))
                .Cast<IGrpcLoadBalancingPolicyProvider>()
                .ToArray();
        }
    }
}
