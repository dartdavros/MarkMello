param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir,

    [Parameter(Mandatory = $true)]
    [ValidateSet("win-x64", "win-arm64")]
    [string]$RuntimeId,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$OutputDir = (Join-Path $PSScriptRoot "dist"),

    [string]$ReleaseOwner = "dartdavros",

    [string]$ReleaseRepo = "MarkMello"
)

$ErrorActionPreference = "Stop"

$scriptPath = Join-Path $PSScriptRoot "MarkMello.iss"
if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
    throw "Inno Setup script not found: $scriptPath"
}

$publishPath = Resolve-Path -LiteralPath $PublishDir
$outputPath = [System.IO.Path]::GetFullPath($OutputDir)
New-Item -ItemType Directory -Force -Path $outputPath | Out-Null

$architecturesAllowed = if ($RuntimeId -eq "win-arm64") { "arm64" } else { "x64compatible" }
$outputBaseName = "MarkMello-setup-$RuntimeId"

$candidateCompilers = @(
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
) | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) }

$iscc = $candidateCompilers | Select-Object -First 1
if (-not $iscc) {
    throw "ISCC.exe was not found. Install Inno Setup 6 first."
}

& $iscc `
    "/DMyPublishDir=$publishPath" `
    "/DMyAppVersion=$Version" `
    "/DMyArchSuffix=$RuntimeId" `
    "/DMyOutputDir=$outputPath" `
    "/DMyOutputBaseName=$outputBaseName" `
    "/DMyArchitecturesAllowed=$architecturesAllowed" `
    "/DMyArchitecturesInstallMode=$architecturesAllowed" `
    "/DMyReleaseOwner=$ReleaseOwner" `
    "/DMyReleaseRepo=$ReleaseRepo" `
    $scriptPath
