param(
    [Parameter(Mandatory = $true)][string]$ArtifactDirectory,
    [Parameter(Mandatory = $true)][string]$FileName,
    [Parameter(Mandatory = $true)][string]$ProductVersion,
    [Parameter(Mandatory = $true)][string]$FileVersion
)

$ErrorActionPreference = 'Stop'
$verify = Join-Path $PSScriptRoot '../../packaging/windows/verify-metadata.ps1'
$arguments = @{
    Directory = $ArtifactDirectory
    FileNames = @($FileName)
    ProductVersion = $ProductVersion
    FileVersion = $FileVersion
}
& $verify @arguments

foreach ($property in @('ProductVersion', 'FileVersion')) {
    $invalid = $arguments.Clone()
    $invalid[$property] = '9999.9999.9999.9999'
    $rejected = $false
    try { & $verify @invalid }
    catch {
        if ($_.Exception.Message -notlike "Unexpected $property for*") { throw }
        $rejected = $true
    }
    if (-not $rejected) { throw "A wrong $property was accepted." }
    Write-Host "PASS: a wrong $property is rejected for the actual build."
}

$missing = $arguments.Clone()
$missing.FileNames = @('MarkMello-required-missing.exe')
$rejected = $false
try { & $verify @missing }
catch {
    if ($_.Exception.Message -notlike 'Required signing file is missing*') { throw }
    $rejected = $true
}
if (-not $rejected) { throw 'A missing required binary was accepted.' }
Write-Host 'PASS: a missing required binary is rejected.'
