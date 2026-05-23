param(
    [switch]$SkipTransparencySample,
    [switch]$SkipPack,
    [switch]$RunBenchmarkDry,
    [int]$KeepBinlogs = 5
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Invoke-LoggedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Command
    )

    Write-Host "==> $Name"
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }
}

function Assert-NoVirtualSolutionFolders {
    $solutionText = Get-Content -Path (Join-Path $root 'ModernOverlay.sln') -Raw
    if ($solutionText -match '2150E333-8FDC-42A3-9474-1A3956D46DE8' -or $solutionText -match 'GlobalSection\(NestedProjects\)') {
        throw 'ModernOverlay.sln contains virtual solution folders or nested-project entries. Use --in-root when adding projects.'
    }
}

function Remove-OldBinlogs {
    param([int]$Count)

    if ($Count -lt 1) {
        throw 'KeepBinlogs must be at least 1.'
    }

    $old = Get-ChildItem -Path $root -Filter *.binlog -File |
        Sort-Object LastWriteTime -Descending |
        Select-Object -Skip $Count

    foreach ($file in $old) {
        if ($file.DirectoryName -ne $root) {
            throw "Refusing to delete outside repository root: $($file.FullName)"
        }

        Remove-Item -LiteralPath $file.FullName
    }
}

function Remove-PackageArtifacts {
    $src = Join-Path $root 'src'
    $packages = Get-ChildItem -Path $src -Recurse -Filter *.nupkg -File

    foreach ($package in $packages) {
        if (!$package.FullName.StartsWith($src, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to delete package outside src directory: $($package.FullName)"
        }

        Remove-Item -LiteralPath $package.FullName
    }
}

function Assert-PackageBoundary {
    $releasePackages = Get-ChildItem -Path (Join-Path $root 'src') -Recurse -Filter *.nupkg -File |
        Where-Object { $_.FullName -like "*\bin\Release\*" }

    $packageNames = $releasePackages | ForEach-Object { $_.Name } | Sort-Object
    $expectedPackageIds = @(
        'ModernOverlay',
        'ModernOverlay.Diagnostics',
        'ModernOverlay.Direct2D',
        'ModernOverlay.Integration',
        'ModernOverlay.Win32'
    )

    if ($packageNames.Count -ne $expectedPackageIds.Count) {
        throw "Expected $($expectedPackageIds.Count) release packages, found $($packageNames.Count): $($packageNames -join ', ')"
    }

    foreach ($packageId in $expectedPackageIds) {
        $pattern = '^' + [Regex]::Escape($packageId) + '\.\d'
        $matches = @($packageNames | Where-Object { $_ -match $pattern })
        if ($matches.Count -ne 1) {
            throw "Expected exactly one release package for '$packageId', found $($matches.Count): $($matches -join ', ')"
        }
    }

    if ($packageNames | Where-Object { $_.StartsWith('ModernOverlay.Integration.Experimental.', [StringComparison]::Ordinal) }) {
        throw 'ModernOverlay.Integration.Experimental must remain source-only and must not be packed for alpha.'
    }

    $modernOverlayPackage = $releasePackages |
        Where-Object { $_.Name.StartsWith('ModernOverlay.', [StringComparison]::Ordinal) -and $_.Name -notmatch '^ModernOverlay\.(Diagnostics|Direct2D|Integration|Win32)\.' } |
        Select-Object -First 1
    if ($null -eq $modernOverlayPackage) {
        throw 'ModernOverlay package was not produced.'
    }

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    foreach ($package in $releasePackages) {
        $zip = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
        try {
            $entries = @($zip.Entries | ForEach-Object { $_.FullName })
            if ($entries -notcontains 'README.md') {
                throw "$($package.Name) is missing the package README."
            }

            $nuspecEntry = $zip.Entries |
                Where-Object { $_.FullName.EndsWith('.nuspec', [StringComparison]::OrdinalIgnoreCase) } |
                Select-Object -First 1
            if ($null -eq $nuspecEntry) {
                throw "$($package.Name) is missing a nuspec entry."
            }

            $reader = [System.IO.StreamReader]::new($nuspecEntry.Open())
            try {
                $nuspec = $reader.ReadToEnd()
            }
            finally {
                $reader.Dispose()
            }

            if ($nuspec -notmatch 'net11\.0-windows' -or $nuspec -notmatch 'DWM/color-key fallback') {
                throw "$($package.Name) release notes do not include the required alpha caveats."
            }
        }
        finally {
            $zip.Dispose()
        }
    }

    $zip = [System.IO.Compression.ZipFile]::OpenRead($modernOverlayPackage.FullName)
    try {
        $entries = @($zip.Entries | ForEach-Object { $_.FullName })
        $requiredEntries = @(
            'lib/net11.0-windows7.0/ModernOverlay.dll',
            'lib/net11.0-windows7.0/ModernOverlay.Direct2D.dll',
            'lib/net11.0-windows7.0/ModernOverlay.Direct2D.xml'
        )

        foreach ($entry in $requiredEntries) {
            if ($entries -notcontains $entry) {
                throw "ModernOverlay package is missing required entry '$entry'."
            }
        }
    }
    finally {
        $zip.Dispose()
    }
}

function Invoke-PackageConsumerSmoke {
    $smokeRoot = Join-Path $root 'artifacts\package-consumer-smoke'
    $packageSource = Join-Path $smokeRoot 'packages'
    $projectDirectory = Join-Path $smokeRoot 'ConsumerApp'

    if (Test-Path $smokeRoot) {
        $resolved = (Resolve-Path $smokeRoot).Path
        $artifactsRoot = Join-Path $root 'artifacts'
        if (!$resolved.StartsWith($artifactsRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to clean package consumer smoke directory outside artifacts: $resolved"
        }

        Remove-Item -LiteralPath $resolved -Recurse -Force
    }

    New-Item -ItemType Directory -Path $packageSource -Force | Out-Null
    New-Item -ItemType Directory -Path $projectDirectory -Force | Out-Null

    $releasePackages = Get-ChildItem -Path (Join-Path $root 'src') -Recurse -Filter *.nupkg -File |
        Where-Object { $_.FullName -like "*\bin\Release\*" }
    foreach ($package in $releasePackages) {
        Copy-Item -LiteralPath $package.FullName -Destination $packageSource
    }

    $directoryBuildProps = Join-Path $projectDirectory 'Directory.Build.props'
    @'
<Project>
  <PropertyGroup>
    <LangVersion>preview</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>
</Project>
'@ | Set-Content -Path $directoryBuildProps -Encoding UTF8

    $directoryPackagesProps = Join-Path $projectDirectory 'Directory.Packages.props'
    @'
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>
</Project>
'@ | Set-Content -Path $directoryPackagesProps -Encoding UTF8

    $nugetConfig = Join-Path $projectDirectory 'NuGet.config'
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-packages" value="$packageSource" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@ | Set-Content -Path $nugetConfig -Encoding UTF8

    $projectFile = Join-Path $projectDirectory 'ConsumerApp.csproj'
    @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net11.0-windows</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="ModernOverlay" Version="0.1.0-preview" />
  </ItemGroup>
</Project>
'@ | Set-Content -Path $projectFile -Encoding UTF8

    $programFile = Join-Path $projectDirectory 'Program.cs'
    @'
using ModernOverlay;
using ModernOverlay.Drawing;
using ModernOverlay.Windows;

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

var options = new OverlayWindowOptions
{
    Title = "Package Consumer Smoke",
    Bounds = WindowBounds.FromPixels(10, 20, 320, 180),
    InputMode = OverlayInputMode.ClickThrough,
    ZOrder = OverlayZOrder.TopMost,
    FrameRateLimit = FrameRateLimit.Fixed(60),
    TransparencyMode = TransparencyMode.Auto,
    Target = WindowTarget.ByTitle("Package Consumer Smoke Target", MatchMode.Exact)
        .WithBoundsMode(TargetBoundsMode.ClientArea),
    ExcludeFromCapture = true,
};

RectF rectangle = new(0, 0, 100, 50);
ColorRgba color = ColorRgba.White;

Require(typeof(OverlayWindow).Namespace == "ModernOverlay", "OverlayWindow must remain in the root facade namespace.");
Require(typeof(OverlayWindowOptions).Namespace == "ModernOverlay.Windows", "OverlayWindowOptions must be in ModernOverlay.Windows.");
Require(typeof(WindowTarget).Namespace == "ModernOverlay.Windows", "WindowTarget must be in ModernOverlay.Windows.");
Require(typeof(RectF).Namespace == "ModernOverlay.Drawing", "RectF must be in ModernOverlay.Drawing.");
Require(typeof(ColorRgba).Namespace == "ModernOverlay.Drawing", "ColorRgba must be in ModernOverlay.Drawing.");
Require(typeof(WindowTarget).GetMethod("ByWindowTitle") is null, "WindowTarget.ByWindowTitle must not return as a v1 alias.");
Require(typeof(WindowTarget).GetMethod("Foreground", Type.EmptyTypes) is null, "WindowTarget.Foreground must not return as a v1 alias.");
Require(!Enum.GetNames<RenderExceptionPolicy>().Contains("IgnoreAndContinue"), "RenderExceptionPolicy.IgnoreAndContinue must not return as a v1 alias.");
Require(options.Bounds == new WindowBounds(10, 20, 320, 180), "Package consumer bounds factory failed.");
Require(rectangle.Width == 100 && color.A == 1f, "Package consumer drawing primitives failed.");

Console.WriteLine("Package consumer smoke passed.");
'@ | Set-Content -Path $programFile -Encoding UTF8

    Invoke-LoggedCommand 'Package consumer restore' {
        dotnet restore $projectFile --configfile $nugetConfig
    }

    Invoke-LoggedCommand 'Package consumer build' {
        dotnet build $projectFile --configuration Release --no-restore
    }

    Invoke-LoggedCommand 'Package consumer run' {
        dotnet run --project $projectFile --configuration Release --no-build
    }

    $outputDirectory = Join-Path $projectDirectory 'bin\Release\net11.0-windows'
    $bundledBackend = Join-Path $outputDirectory 'ModernOverlay.Direct2D.dll'
    if (!(Test-Path $bundledBackend)) {
        throw "Package consumer output is missing bundled Direct2D backend: $bundledBackend"
    }
}

Assert-NoVirtualSolutionFolders

Invoke-LoggedCommand 'Build' {
    dotnet build .\ModernOverlay.sln --configuration Release --no-restore -m:1 -bl:{{}}
}

Invoke-LoggedCommand 'Full test suite' {
    dotnet test .\ModernOverlay.sln --configuration Release --no-build --logger trx -bl:{{}}
}

Invoke-LoggedCommand 'Non-integration test suite' {
    dotnet test .\ModernOverlay.sln --configuration Release --no-build --filter "TestCategory!=WindowsIntegration" --logger trx -bl:{{}}
}

if (-not $SkipPack) {
    Remove-PackageArtifacts

    Invoke-LoggedCommand 'Pack' {
        dotnet pack .\ModernOverlay.sln --configuration Release --no-build -m:1 -bl:{{}}
    }

    Assert-PackageBoundary

    Invoke-PackageConsumerSmoke
}

if (-not $SkipTransparencySample) {
    Invoke-LoggedCommand 'Transparency sample' {
        dotnet run --project .\samples\TransparencyValidationOverlay\TransparencyValidationOverlay.csproj --configuration Release --no-build
    }
}

if ($RunBenchmarkDry) {
    $benchmarkDirectory = Join-Path $root 'benchmarks\ModernOverlay.Benchmarks'
    $benchmarkLog = Join-Path $benchmarkDirectory 'benchmark-dryrun-latest.log'
    Push-Location $benchmarkDirectory
    try {
        Invoke-LoggedCommand 'Benchmark dry run' {
            dotnet run --project .\ModernOverlay.Benchmarks.csproj --configuration Release --no-build -- --filter "*" --job Dry --noOverwrite *> $benchmarkLog
        }

        $issuePatterns = 'Benchmarks with issues', '// Build Error', '// Exception', 'No benchmarks were found', 'No loggers defined'
        foreach ($pattern in $issuePatterns) {
            if (Select-String -Path $benchmarkLog -Pattern $pattern -Quiet) {
                throw "Benchmark dry run log contains '$pattern'. See $benchmarkLog."
            }
        }
    }
    finally {
        Pop-Location
    }
}

Remove-OldBinlogs -Count $KeepBinlogs

Write-Host 'Release validation command gate completed.'
