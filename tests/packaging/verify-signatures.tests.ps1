param(
    [Parameter(Mandatory = $true)]
    [string]$UnsignedAppDirectory
)

$ErrorActionPreference = 'Stop'
$repo = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$verify = Join-Path $repo 'packaging/windows/verify-signatures.ps1'
Add-Type -Path (Join-Path $repo 'packaging/windows/AuthenticodeVerifier.cs')
$signedFile = (Get-Command dotnet -ErrorAction Stop).Source
$result = [MarkMello.Packaging.AuthenticodeVerifier]::Verify($signedFile)
if ($result -ne 0) {
    throw ('Known Microsoft-signed executable failed verification: 0x{0:X8}' -f $result)
}
Write-Host 'PASS: native Authenticode verification accepts a real signed executable.'

foreach ($policy in @('test-signing', 'release-signing')) {
    try {
        & $verify -Directory (Split-Path $signedFile) `
            -FileNames @([System.IO.Path]::GetFileName($signedFile)) -SigningPolicy $policy
        throw "Wrong signer was accepted for $policy."
    } catch {
        if ($_.Exception.Message -notlike 'Unexpected signing certificate*') { throw }
        Write-Host "PASS: $policy rejects a different valid signer."
    }
}

try {
    & $verify -Directory (Split-Path $signedFile) `
        -FileNames @('MarkMello-required-missing.exe') -SigningPolicy test-signing
    throw 'Missing required file was accepted.'
} catch {
    if ($_.Exception.Message -notlike 'Required signed file is missing*') { throw }
    Write-Host 'PASS: a missing required file is rejected.'
}

$unsignedApp = Join-Path $UnsignedAppDirectory 'MarkMello.exe'
if (-not (Test-Path -LiteralPath $unsignedApp -PathType Leaf)) {
    throw "Build the actual unsigned application first: $unsignedApp"
}
try {
    & $verify -Directory $UnsignedAppDirectory -FileNames @('MarkMello.exe') -SigningPolicy test-signing
    throw 'Unsigned application was accepted.'
} catch {
    if ($_.Exception.Message -notlike 'No Authenticode signer certificate*') { throw }
    Write-Host 'PASS: the actual unsigned application is rejected.'
}
