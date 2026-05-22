using ModernOverlay.Direct2D;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class VorticeDependencyProbeTests
{
    private static readonly string[] ExpectedUnresolvedSpecAssemblies = ["Vortice.DirectWrite", "Vortice.WIC"];

    [TestMethod]
    public void RequiredVorticeAssembliesLoad()
    {
        IReadOnlyList<string> loaded = VorticeDependencyProbe.LoadRequiredAssemblies();

        CollectionAssert.IsSubsetOf(VorticeDependencyProbe.RequiredAssemblyNames.ToArray(), loaded.ToArray());
        CollectionAssert.AreEquivalent(ExpectedUnresolvedSpecAssemblies, VorticeDependencyProbe.UnresolvedSpecAssemblyNames.ToArray());
    }
}
