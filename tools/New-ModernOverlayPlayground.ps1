param(
    [string]$From = 'BasicOverlay',
    [string]$Name,
    [switch]$List
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot

$samples = [ordered]@{
    Basic = 'BasicOverlay'
    BasicOverlay = 'BasicOverlay'
    Diagnostics = 'DiagnosticsOverlay'
    DiagnosticsOverlay = 'DiagnosticsOverlay'
    Geometry = 'GeometryOverlay'
    GeometryOverlay = 'GeometryOverlay'
    Hotkey = 'HotkeyOverlay'
    HotkeyOverlay = 'HotkeyOverlay'
    Image = 'ImageOverlay'
    ImageOverlay = 'ImageOverlay'
    ImageAndText = 'ImageAndTextOverlay'
    ImageAndTextOverlay = 'ImageAndTextOverlay'
    Input = 'InputModeOverlay'
    InputMode = 'InputModeOverlay'
    InputModeOverlay = 'InputModeOverlay'
    Interactive = 'InteractiveOverlay'
    InteractiveOverlay = 'InteractiveOverlay'
    Ipc = 'IpcOverlayDemo'
    IpcOverlayDemo = 'IpcOverlayDemo'
    OwnedHost = 'SampleOwnedHost'
    SampleOwnedHost = 'SampleOwnedHost'
    Showcase = 'ShowcaseOverlay'
    ShowcaseOverlay = 'ShowcaseOverlay'
    Shapes = 'ShapesOverlay'
    ShapesOverlay = 'ShapesOverlay'
    Sticky = 'StickyTargetOverlay'
    StickyTarget = 'StickyTargetOverlay'
    StickyTargetOverlay = 'StickyTargetOverlay'
    StickyWindow = 'StickyWindowOverlay'
    StickyWindowOverlay = 'StickyWindowOverlay'
    Text = 'TextLayoutOverlay'
    TextLayout = 'TextLayoutOverlay'
    TextLayoutOverlay = 'TextLayoutOverlay'
    Transparency = 'TransparencyValidationOverlay'
    TransparencyValidation = 'TransparencyValidationOverlay'
    TransparencyValidationOverlay = 'TransparencyValidationOverlay'
}

function Write-SampleList {
    $samples.Values |
        Select-Object -Unique |
        Sort-Object |
        ForEach-Object { $_ }
}

if ($List) {
    Write-SampleList
    return
}

if (!$samples.Contains($From)) {
    Write-Host "Unknown sample '$From'. Available samples:"
    Write-SampleList
    exit 1
}

$sourceSample = $samples[$From]
$sourceDirectory = Join-Path $root "samples\$sourceSample"
if (!(Test-Path $sourceDirectory)) {
    throw "Sample directory was not found: $sourceDirectory"
}

$sourceProject = Join-Path $sourceDirectory "$sourceSample.csproj"
if (!(Test-Path $sourceProject)) {
    throw "Sample project was not found: $sourceProject"
}

if ([string]::IsNullOrWhiteSpace($Name)) {
    $Name = "$sourceSample-Playground"
}

if ($Name -notmatch '^[A-Za-z0-9_.-]+$') {
    throw 'Playground name may only contain letters, numbers, dot, underscore, and dash.'
}

$playgroundRoot = Join-Path $root 'artifacts\playgrounds'
$targetDirectory = Join-Path $playgroundRoot $Name
if (Test-Path $targetDirectory) {
    throw "Playground already exists: $targetDirectory"
}

New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null

Copy-Item -Path (Join-Path $sourceDirectory '*') -Destination $targetDirectory -Recurse

$targetProject = Join-Path $targetDirectory "$Name.csproj"
$copiedProject = Join-Path $targetDirectory "$sourceSample.csproj"
if (Test-Path $copiedProject) {
    Remove-Item -LiteralPath $copiedProject
}

[xml]$sourceProjectXml = Get-Content -Path $sourceProject -Raw

$localProgram = Join-Path $targetDirectory 'Program.cs'
if (!(Test-Path $localProgram)) {
    $linkedProgram = $sourceProjectXml.Project.ItemGroup.Compile |
        Where-Object { $_.Link -eq 'Program.cs' -or [System.IO.Path]::GetFileName($_.Include) -eq 'Program.cs' } |
        Select-Object -First 1

    if ($null -ne $linkedProgram) {
        $linkedProgramPath = Join-Path $sourceDirectory $linkedProgram.Include
        $resolvedProgram = (Resolve-Path $linkedProgramPath).Path
        Copy-Item -LiteralPath $resolvedProgram -Destination $localProgram
    }
}

if (!(Test-Path $localProgram)) {
    throw "Could not find or create a local Program.cs for playground '$Name'."
}

$projectReferences = @($sourceProjectXml.Project.ItemGroup.ProjectReference)
$projectReferenceLines = foreach ($reference in $projectReferences) {
    if ([string]::IsNullOrWhiteSpace($reference.Include)) {
        continue
    }

    $absoluteReference = (Resolve-Path (Join-Path $sourceDirectory $reference.Include)).Path
    $relativeReference = [System.IO.Path]::GetRelativePath($targetDirectory, $absoluteReference)
    "    <ProjectReference Include=""$relativeReference"" />"
}

@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net11.0-windows</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
$($projectReferenceLines -join [Environment]::NewLine)
  </ItemGroup>
</Project>
"@ | Set-Content -Path $targetProject -Encoding UTF8

$readmePath = Join-Path $targetDirectory 'README.md'
@"
# $Name

Scratch ModernOverlay playground copied from `$sourceSample`.

The playground has its own editable `Program.cs` and project references rewritten for the `artifacts\playgrounds` location.

Run it from the repository root:

````powershell
`$env:MSBuildEnableWorkloadResolver='false'
dotnet run --project artifacts\playgrounds\$Name\$Name.csproj --configuration Release
````

This folder is intentionally under `artifacts` so you can compare ideas without changing the canonical samples.
"@ | Set-Content -Path $readmePath -Encoding UTF8

Write-Host "Created playground: $targetDirectory"
Write-Host "Project: $targetProject"
