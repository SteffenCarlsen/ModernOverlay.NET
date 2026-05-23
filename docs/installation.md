# Installation

ModernOverlay is currently a preview Windows-only library targeting `net11.0-windows`.

Package publishing is prepared through the repository release workflow, but the first NuGet publish will happen only after NuGet trusted publishing is configured. See [release publishing](release-publishing.md).

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

The solution includes the spec-named samples `StickyWindowOverlay`, `InteractiveOverlay`, `ImageAndTextOverlay`, and `GeometryOverlay` as linked-source aliases for the more descriptive capability samples `StickyTargetOverlay`, `InputModeOverlay`, `ImageOverlay`, and `ShapesOverlay`.

For package-based consumption, the common path is one package:

```xml
<PackageReference Include="ModernOverlay.NET" Version="1.0.0" />
```

The `ModernOverlay` package includes the Direct2D backend assembly for the preview common path. The facade auto-discovers and registers that backend before creating the first overlay. `Direct2DOverlayBackend.Register()` remains available for tests, custom startup flows, and hosts that want explicit registration.

Advanced hosts can still reference the backend package directly:

```xml
<PackageReference Include="ModernOverlay.NET" Version="1.0.0" />
<PackageReference Include="ModernOverlay.NET.Direct2D" Version="1.0.0" />
```

See [public API and package review](public-api-package-review.md) for the package split review and the options for a future all-in-one package.

## Target Framework

Applications should target `net11.0-windows` while this repository uses the .NET 11 preview path. `main` intentionally does not carry a checked-in `net10.0-windows` fallback.
