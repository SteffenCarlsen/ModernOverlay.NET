using System.Reflection;

namespace ModernOverlay.Direct2D;

public static class VorticeDependencyProbe
{
    private static readonly string[] RequiredAssemblies =
    [
        "Vortice.Direct2D1",
        "Vortice.DXGI",
        "Vortice.DirectX",
        "Vortice.Mathematics",
        "Vortice.Win32",
    ];

    private static readonly string[] UnresolvedSpecAssemblies =
    [
        "Vortice.DirectWrite",
        "Vortice.WIC",
    ];

    public static IReadOnlyList<string> RequiredAssemblyNames => RequiredAssemblies;

    public static IReadOnlyList<string> UnresolvedSpecAssemblyNames => UnresolvedSpecAssemblies;

    public static IReadOnlyList<string> LoadRequiredAssemblies()
    {
        var loaded = new List<string>(RequiredAssemblies.Length);

        foreach (string assemblyName in RequiredAssemblies)
        {
            Assembly assembly = Assembly.Load(new AssemblyName(assemblyName));
            loaded.Add(assembly.GetName().Name ?? assemblyName);
        }

        return loaded;
    }
}
