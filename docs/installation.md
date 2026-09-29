# Installation

ModernOverlay.NET is a Windows-only library targeting `net11.0-windows` on prerelease .NET 11.

The latest published version is [1.1.3](https://github.com/SteffenCarlsen/ModernOverlay.NET/releases/tag/v1.1.3), available on [NuGet](https://www.nuget.org/packages/ModernOverlay.NET/1.1.3). See [release publishing](release-publishing.md) for the publishing workflow.

## Requirements

- Windows desktop.
- A .NET 11 SDK. Repository development uses .NET 11 RC1, `11.0.100-rc.1.26425.128`, as selected by [global.json](../global.json).
- A Windows-capable IDE or command line environment.

## Project References

During repository development, samples reference projects directly:

```xml
<ProjectReference Include="..\..\src\ModernOverlay\ModernOverlay.csproj" />
<ProjectReference Include="..\..\src\ModernOverlay.Direct2D\ModernOverlay.Direct2D.csproj" />
```

The solution includes the spec-named samples `StickyWindowOverlay`, `InteractiveOverlay`, `ImageAndTextOverlay`, and `GeometryOverlay` as linked-source aliases for the more descriptive capability samples `StickyTargetOverlay`, `InputModeOverlay`, `ImageOverlay`, and `ShapesOverlay`.

For package-based consumption, the common path is one package:

```xml
<PackageReference Include="ModernOverlay.NET" Version="1.1.3" />
```

The `ModernOverlay.NET` package includes the `ModernOverlay.Direct2D` backend assembly for the common path. The facade auto-discovers and registers that backend before creating the first overlay. `Direct2DOverlayBackend.Register()` remains available for tests, custom startup flows, and hosts that want explicit registration.

Advanced hosts can still reference the backend package directly:

```xml
<PackageReference Include="ModernOverlay.NET" Version="1.1.3" />
<PackageReference Include="ModernOverlay.NET.Direct2D" Version="1.1.3" />
```

For retained interactive controls, add `ModernOverlay.UI` at the same version. See the [package list](../README.md#packages) for all published IDs. These NuGet IDs differ from the `ModernOverlay` and `ModernOverlay.*` C# namespaces and assembly names.

## Target Framework

Applications should target `net11.0-windows` while this repository uses the .NET 11 prerelease path. `main` intentionally does not carry a checked-in `net10.0-windows` fallback.
