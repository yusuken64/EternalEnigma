using System.Reflection;
using System.Runtime.Versioning;
using Xunit;

namespace EternalEnigma.Core.Tests.Architecture;

public sealed class CoreAssemblyTests
{
    private static readonly Assembly CoreAssembly = Assembly.Load("EternalEnigma.Core");

    [Fact]
    public void LibraryTargetsNetStandard21()
    {
        Assert.Equal(".NETStandard,Version=v2.1",
            CoreAssembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName);
    }

    [Fact]
    public void DependencyGraphDoesNotReferenceUnityOrTileWorldCreator()
    {
        var pending = new Stack<Assembly>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        pending.Push(CoreAssembly);

        while (pending.Count > 0)
        {
            var assembly = pending.Pop();
            if (!visited.Add(assembly.FullName!)) continue;

            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                var name = reference.Name!;
                Assert.False(IsEngineAssembly(name), $"{assembly.GetName().Name} references {name}.");
                pending.Push(Assembly.Load(reference));
            }
        }
    }

    private static bool IsEngineAssembly(string name) =>
        name.StartsWith("Unity", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("TileWorldCreator", StringComparison.OrdinalIgnoreCase);
}
