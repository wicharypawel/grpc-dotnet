using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;

namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    /// <summary>
    /// An XdsClientObjectPool holding reference and referenceCount of an <seealso cref="XdsClient"/> instance.
    /// 
    /// This class is used to manage the life cycle of an object. The need to count references is 
    /// due to the fact that an object is created in one place in the system and then used in several 
    /// others, so it is not possible to easily determine the place responsible for its disposal.
    /// </summary>
    internal sealed class XdsClientObjectPool
    {
        private readonly object LockObject = new object();
        private readonly ILoggerFactory _loggerFactory;
        private IXdsClient? xdsClient;
        private int referenceCount;

        public XdsClientObjectPool(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
            xdsClient = null;
            referenceCount = 0;
        }

        /// <summary>
        /// Increments the referenceCount and returns the cached instance of <seealso cref="XdsClient"/>.
        /// </summary>
        /// <returns>Instance of <seealso cref="XdsClient"/>.</returns>
        public IXdsClient GetObject()
        {
            lock (LockObject)
            {
                if (xdsClient == null)
                {
                    if (referenceCount != 0)
                    {
                        throw new InvalidOperationException("referenceCount should be zero while xdsClient is null");
                    }
                    xdsClient = XdsClientFactory.CreateXdsClient(_loggerFactory);
                }
                referenceCount++;
                return xdsClient;
            }
        }

        /// <summary>
        /// Return the object to the pool and decrements the referenceCount. The caller should not use the object beyond this point. 
        /// Anytime when the referenceCount gets back to zero, the XdsClient instance will be Disposed and de-referenced. 
        /// </summary>
        /// <param name="instance">Instance of <seealso cref="XdsClient"/> that was previously obtained from this pool.</param>
        public void ReturnObject(IXdsClient instance)
        {
            lock (LockObject)
            {
                if (xdsClient != instance)
                {
                    throw new InvalidOperationException("the returned instance does not match current XdsClient");
                }
                referenceCount--;
                if (referenceCount < 0)
                {
                    throw new InvalidOperationException("referenceCount of XdsClient less than 0");
                }
                if (referenceCount == 0)
                {
                    xdsClient.Dispose();
                    xdsClient = null;
                }
            }
        }
    }
}
