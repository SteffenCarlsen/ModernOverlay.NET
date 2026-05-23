param(
    [string]$Sample = 'BasicOverlay',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$List,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$SampleArgs
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

if (!$samples.Contains($Sample)) {
    Write-Host "Unknown sample '$Sample'. Available samples:"
    Write-SampleList
    exit 1
}

$sampleName = $samples[$Sample]
$projectPath = Join-Path $root "samples\$sampleName\$sampleName.csproj"
if (!(Test-Path $projectPath)) {
    throw "Sample project was not found: $projectPath"
}

$env:MSBuildEnableWorkloadResolver = 'false'

$arguments = @(
    'run',
    '--project',
    $projectPath,
    '--configuration',
    $Configuration
)

if ($SampleArgs.Count -gt 0) {
    $arguments += '--'
    $arguments += $SampleArgs
}

dotnet @arguments
exit $LASTEXITCODE
