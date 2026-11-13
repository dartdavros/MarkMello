param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir,

    [Parameter(Mandatory = $true)]
    [ValidateSet("win-x64", "win-arm64")]
    [string]$RuntimeId,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$WindowsVersion,

    [string]$OutputDir = (Join-Path $PSScriptRoot "dist"),

    [string]$SetupIconPath = (Join-Path $PSScriptRoot "markmello-installer.ico"),

    [string]$ReleaseOwner = "dartdavros",

    [string]$ReleaseRepo = "MarkMello"
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command python -CommandType Application -ErrorAction SilentlyContinue)) {
    throw 'Python 3.9 or later is required on PATH to normalize installer metadata.'
}

if (-not $WindowsVersion) {
    if ($Version -notmatch '^(\d+)\.(\d+)\.(\d+)(?:[+-].*)?$') {
        throw 'Provide WindowsVersion as a four-part Windows version.'
    }
    $WindowsVersion = "$($Matches[1]).$($Matches[2]).$($Matches[3]).0"
}
if ($WindowsVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw 'WindowsVersion must contain exactly four numeric parts.'
}

$scriptPath = Join-Path $PSScriptRoot "MarkMello.iss"
if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
    throw "Inno Setup script not found: $scriptPath"
}

$publishPath = Resolve-Path -LiteralPath $PublishDir
$outputPath = [System.IO.Path]::GetFullPath($OutputDir)
$setupIcon = Resolve-Path -LiteralPath $SetupIconPath
New-Item -ItemType Directory -Force -Path $outputPath | Out-Null

$architecturesAllowed = if ($RuntimeId -eq "win-arm64") { "arm64" } else { "x64compatible" }
$outputBaseName = "MarkMello-setup-$RuntimeId"

# The machine-wide locations come first because that is where CI installs
# Inno Setup. The per-user one is what an unelevated `winget install
# JRSoftware.InnoSetup` produces, which is the common case on a dev box.
$candidateCompilers = @(
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
) | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) }

$iscc = $candidateCompilers | Select-Object -First 1
if (-not $iscc) {
    $iscc = (Get-Command "ISCC.exe" -ErrorAction SilentlyContinue).Source
}

if (-not $iscc) {
    throw "ISCC.exe was not found. Install Inno Setup 6 first, or put ISCC.exe on PATH."
}

& $iscc `
    "/DMyPublishDir=$publishPath" `
    "/DMyAppVersion=$Version" `
    "/DMyWindowsVersion=$WindowsVersion" `
    "/DMyArchSuffix=$RuntimeId" `
    "/DMyOutputDir=$outputPath" `
    "/DMyOutputBaseName=$outputBaseName" `
    "/DMySetupIconFile=$setupIcon" `
    "/DMyArchitecturesAllowed=$architecturesAllowed" `
    "/DMyArchitecturesInstallMode=$architecturesAllowed" `
    "/DMyReleaseOwner=$ReleaseOwner" `
    "/DMyReleaseRepo=$ReleaseRepo" `
    $scriptPath
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed with exit code $LASTEXITCODE" }

& python (Join-Path $PSScriptRoot 'normalize-installer-metadata.py') `
    (Join-Path $outputPath "$outputBaseName.exe") `
    --product-version $Version --file-version $WindowsVersion
if ($LASTEXITCODE -ne 0) { throw 'Failed to normalize installer version metadata before signing.' }
