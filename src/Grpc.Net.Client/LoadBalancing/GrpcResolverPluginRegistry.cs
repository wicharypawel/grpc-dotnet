using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// Registry of <seealso cref="IGrpcResolverPluginProvider"/>s. 
    /// </summary>
    public sealed class GrpcResolverPluginRegistry
    {
        private readonly ConcurrentDictionary<string, IGrpcResolverPluginProvider> _providers = new ConcurrentDictionary<string, IGrpcResolverPluginProvider>();

        private GrpcResolverPluginRegistry()
        {
        }

        private static GrpcResolverPluginRegistry? Instance;
        private static readonly object LockObject = new object();

        /// <summary>
        /// Register provider.
        /// </summary>
        public void Register(IGrpcResolverPluginProvider provider)
        {
            if (!_providers.TryAdd(provider.Scheme, provider))
            {
                throw new InvalidOperationException("Registering resolver plugin provider failed");
            }
        }

        /// <summary>
        /// Deregisters provider.
        /// </summary>
        public void Deregister(IGrpcResolverPluginProvider provider)
        {
            if(!_providers.TryRemove(provider.Scheme, out _))
            {
                throw new InvalidOperationException("Deregistering resolver plugin provider failed");
            }
        }

        /// <summary>
        /// Returns the provider for the given scheme. 
        /// </summary>
        /// <param name="scheme">Scheme used for target written eg. http, dns, xds etc.</param>
        /// <returns>ResolverPluginProvider or null if no suitable provider can be found.</returns>
        public IGrpcResolverPluginProvider? GetProvider(string scheme)
        {
            if(_providers.TryGetValue(scheme, out var resolverPluginProvider))
            {
                return resolverPluginProvider;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the default registry.
        /// </summary>
        /// <returns>Instance of <seealso cref="GrpcResolverPluginRegistry"/>.</returns>
        public static GrpcResolverPluginRegistry GetDefaultRegistry()
        {
            return GetDefaultRegistry(NullLoggerFactory.Instance);
        }

        /// <summary>
        /// Returns the default registry.
        /// </summary>
        /// <param name="loggerFactory">Logger factory instance.</param>
        /// <returns>Instance of <seealso cref="GrpcResolverPluginRegistry"/>.</returns>
        public static GrpcResolverPluginRegistry GetDefaultRegistry(ILoggerFactory loggerFactory)
        {
            if (Instance != null)
            {
                return Instance;
            }
            lock (LockObject)
            {
                if (Instance == null)
                {
                    var logger = loggerFactory.CreateLogger<GrpcResolverPluginRegistry>();
                    Instance = new GrpcResolverPluginRegistry();
                    logger.LogDebug($"{nameof(GrpcResolverPluginRegistry)} created");
                    foreach (var provider in GetResolverProvidersFromAppDomain())
                    {
                        Instance.Register(provider);
                        logger.LogDebug($"{nameof(GrpcResolverPluginRegistry)} found {provider.GetType().Name} for scheme {provider.Scheme}");
                    }
                }
                return Instance;
            }
        }

        private static IGrpcResolverPluginProvider[] GetResolverProvidersFromAppDomain()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic)
                .Where(assembly => assembly.FullName.StartsWith("Grpc.Net.Client", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return assemblies.SelectMany(GetResolverProvidersFromAssembly).ToArray();
        }

        private static IGrpcResolverPluginProvider[] GetResolverProvidersFromAssembly(Assembly assembly)
        {
            return assembly.GetTypes()
                .Where(type => type.IsClass && !type.IsAbstract)
                .Where(type => typeof(IGrpcResolverPluginProvider).IsAssignableFrom(type))
                .Select(type => Activator.CreateInstance(type))
                .Cast<IGrpcResolverPluginProvider>()
                .ToArray();
        }
    }
}
