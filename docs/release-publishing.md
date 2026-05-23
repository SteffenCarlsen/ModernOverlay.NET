# Release Publishing

This repository publishes packages through a tag-driven GitHub Actions workflow and NuGet trusted publishing.

## Version Source

Release tags should use a SemVer value with a leading `v`, for example:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

The workflow strips the leading `v` and passes the result to MSBuild as `Version` and `PackageVersion`. NuGet package versions are immutable, so every published release must use a new version.

Manual workflow runs can also provide a version directly. Use this for dry release packaging or emergency republishing of GitHub release assets.

## NuGet Setup

The workflow can create the GitHub release before NuGet publishing is enabled. NuGet publishing starts automatically for tag releases once the matching trusted publishing policy exists on nuget.org.

| NuGet trusted publishing field | Value |
|---|---|
| Package Owner | `TaFFe` |
| Repository Owner | `SteffenCarlsen` |
| Repository | `ModernOverlay.NET` |
| Workflow File | `release.yml` |
| Environment | `release` |

The workflow uses `NuGet/login@v1` with the `NUGET_USER` repository secret to exchange the GitHub Actions OIDC token for a short-lived NuGet API key at publish time. `NUGET_USER` must be the NuGet profile name, not an email address. No long-lived NuGet API key should be stored in repository secrets for this path.

## Published Packages

The release workflow packs the solution and publishes the same package set validated by the local release gate:

| Package | Published |
|---|---|
| `ModernOverlay.NET` | Yes |
| `ModernOverlay.NET.Direct2D` | Yes |
| `ModernOverlay.NET.Win32` | Yes |
| `ModernOverlay.NET.Diagnostics` | Yes |
| `ModernOverlay.NET.Integration` | Yes |
| `ModernOverlay.Integration.Experimental` | No |

Symbol packages are emitted as `.snupkg` files and pushed with the release packages when NuGet publishing is enabled.

## Release Gate

Before tagging a release, run the local validation gate from the repository root:

```powershell
$env:MSBuildEnableWorkloadResolver='false'
tools\Invoke-ModernOverlayReleaseValidation.ps1 -RunBenchmarkDry
```

Record the result using `docs/release-validation-results-template.md` when the release materially changes package behavior, public API, or supported validation evidence.
