namespace ModernOverlay.Tests;

[TestClass]
public sealed class SharpDxGuardTests
{
    [TestMethod]
    public void SourceAndProjectFilesDoNotReferenceSharpDx()
    {
        string repositoryRoot = FindRepositoryRoot();
        string forbiddenUsing = "using " + "SharpDX";
        string forbiddenPackage = "PackageReference Include=\"" + "SharpDX";
        string[] files = Directory.GetFiles(repositoryRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".props", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".targets", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            Assert.IsFalse(text.Contains(forbiddenUsing, StringComparison.OrdinalIgnoreCase), $"Forbidden SharpDX import in {file}");
            Assert.IsFalse(text.Contains(forbiddenPackage, StringComparison.OrdinalIgnoreCase), $"Forbidden SharpDX package in {file}");
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ModernOverlay.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
