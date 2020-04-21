namespace Grpc.Net.Client.LoadBalancing.Extensions
{
    /// <summary>
    /// Resolvers and policies in this assembly are loaded by registries via reflection. During publish 
    /// .NET Core will fail to detect references and would exclude the library from the published folder.
    /// 
    /// In order to ensure there is at least one direct reference, call this method anywhere in your code. 
    /// </summary>
    public sealed class EnsureLoadAssembly
    {
        /// <summary>
        /// Calling this method will make the assembly available at runtime.
        /// </summary>
        public static void Load()
        {
            // empty
        }
    }
}
