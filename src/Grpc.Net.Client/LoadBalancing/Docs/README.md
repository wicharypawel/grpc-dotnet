# gRPC dotnet internal load balancing documentation

## Types mapping to java implementation

Some time ago, the API has been fully changed to be based on the JAVA API. The aim of this change was to achieve high interoperability between languages and the ability to rely on their implementation approach. 

### Load balancing API
gRPC dotnet type| gRPC java type
---|---
GrpcChannel | ManagedChannelImpl _(ManagedChannelImpl.java)_
IGrpcResolverPluginProvider | Factory _(NameResolver.java)_ and NameResolverProvider _(NameResolverProvider.java)_
IGrpcResolverPlugin | NameResolver _(NameResolver.java)_
IGrpcNameResolutionObserver | Listener2 _(NameResolver.java)_
IGrpcLoadBalancingPolicyProvider | Factory _(LoadBalancer.java)_ and LoadBalancerProvider _(LoadBalancerProvider.java)_
IGrpcLoadBalancingPolicy | LoadBalancer _(LoadBalancer.java)_
IGrpcSubchannelPicker | SubchannelPicker _(LoadBalancer.java)_
IGrpcHelper | Helper _(LoadBalancer.java)_
IGrpcSubchannelStateObserver | SubchannelStateListener _(LoadBalancer.java)_
IGrpcSubChannel | Subchannel _(LoadBalancer.java)_

### Load balancing core concrete classes
gRPC dotnet type| gRPC java type
---|---
GrpcResolverPluginRegistry | NameResolverRegistry _(NameResolverRegistry.java)_
GrpcNameResolutionResult | ResolutionResult _(NameResolver.java)_
GrpcServiceConfigOrError | ConfigOrError _(NameResolver.java)_
GrpcHostAddress | EquivalentAddressGroup _(EquivalentAddressGroup.java)_
GrpcNameResolutionObserver | NameResolverListener _(ManagedChannelImpl.java)_
GrpcLoadBalancingPolicyRegistry | LoadBalancerRegistry _(LoadBalancerRegistry.java)_
GrpcResolvedAddresses | ResolvedAddresses _(LoadBalancer.java)_
GrpcPickResult | PickResult _(LoadBalancer.java)_
GrpcSubChannel | SubchannelImpl _(ManagedChannelImpl.java)_
GrpcConnectivityStateManager | ConnectivityStateManager _(ConnectivityStateManager.java)_
GrpcConnectivityStateInfo | ConnectivityStateInfo _(ConnectivityStateInfo.java)_
GrpcConnectivityState | ConnectivityState _(ConnectivityState.java)_
GrpcAttributes | Attributes _(Attributes.java)_
GrpcHelper | LbHelperImpl _(ManagedChannelImpl.java)_
GrpcSynchronizationContext | SynchronizationContext _(SynchronizationContext.java)_

### Other types ported from JAVA
gRPC dotnet type| gRPC java type
---|---
IGrpcBackoffPolicy | BackoffPolicy _(BackoffPolicy.java)_
IGrpcBackoffPolicyProvider | Provider _(BackoffPolicy.java)_
GrpcExponentialBackoffPolicy | ExponentialBackoffPolicy _(ExponentialBackoffPolicy.java)_
GrpcExponentialBackoffPolicyProvider | Provider _(ExponentialBackoffPolicy.java)_
IGrpcPickSubchannelArgs | PickSubchannelArgs _(LoadBalancer.java)_
GrpcPickSubchannelArgs | PickSubchannelArgsImpl _(PickSubchannelArgsImpl.java)_