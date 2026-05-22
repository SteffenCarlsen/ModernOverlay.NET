# Installation

ModernOverlay is currently a preview Windows-only library targeting `net11.0-windows`.

## Requirements

- Windows desktop.
- .NET 11 preview SDK matching `global.json`.
- A Windows-capable IDE or command line environment.

## Project References

During repository development, samples reference projects directly:

```xml
<ProjectReference Include="..\..\src\ModernOverlay\ModernOverlay.csproj" />
<ProjectReference Include="..\..\src\ModernOverlay.Direct2D\ModernOverlay.Direct2D.csproj" />
```

For package-based consumption, use the core package plus the Direct2D backend package while the package split remains explicit:

```xml
<PackageReference Include="ModernOverlay" Version="0.1.0-preview" />
<PackageReference Include="ModernOverlay.Direct2D" Version="0.1.0-preview" />
```

The current backend registration model requires:

```csharp
Direct2DOverlayBackend.Register();
```

Call this once during application startup before creating overlays.

## Target Framework

Applications should target `net11.0-windows` while this repository uses the .NET 11 preview path. See `docs/adr/0001-dotnet-11-preview-and-net10-contingency.md` for the exact conditions that would move the implementation to `net10.0-windows`.

