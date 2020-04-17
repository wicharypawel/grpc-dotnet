using System;
using System.IO;
using System.Reflection;

namespace Grpc.Net.Client.LoadBalancing.Tests.Policies.Factories
{
    internal static class XdsBootstrapFileFactory
    {
        public static string GetSampleFile(string fileName)
        {
            var assemblyPath = Path.GetDirectoryName(Assembly.GetAssembly(typeof(XdsBootstrapFileFactory))!.Location)
                ?? throw new InvalidOperationException("Assembly location not found");
            var bootstrapFilePath = Path.Combine(assemblyPath, "XdsRelated", "Factories", fileName);
            return File.ReadAllText(bootstrapFilePath);
        }
    }
}
