param(
    [Parameter(Mandatory = $true)]
    [string]$Directory,

    [Parameter(Mandatory = $true)]
    [string[]]$FileNames,

    [string[]]$OptionalFileNames = @(),

    [Parameter(Mandatory = $true)]
    [ValidateSet('test-signing', 'release-signing')]
    [string]$SigningPolicy
)

$ErrorActionPreference = 'Stop'
if (-not ('MarkMello.Packaging.AuthenticodeVerifier' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'AuthenticodeVerifier.cs')
}

$verifiedCount = 0
foreach ($fileName in $FileNames) {
    $path = [System.IO.Path]::GetFullPath((Join-Path $Directory $fileName))
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        if ($fileName -in $OptionalFileNames) { continue }
        throw "Required signed file is missing: $path"
    }

    $signature = Get-AuthenticodeSignature -LiteralPath $path
    if (-not $signature.SignerCertificate) {
        throw "No Authenticode signer certificate: $path"
    }
    $signerName = $signature.SignerCertificate.GetNameInfo(
        [System.Security.Cryptography.X509Certificates.X509NameType]::SimpleName, $false)
    $expectedName = if ($SigningPolicy -eq 'test-signing') {
        "Test certificate for 'MarkMello [OSS]'"
    } else {
        'SignPath Foundation'
    }
    if ($signerName -ne $expectedName) {
        throw "Unexpected signing certificate '$signerName' for $path"
    }

    $result = [MarkMello.Packaging.AuthenticodeVerifier]::Verify($path)
    # A self-signed test certificate may have an untrusted root. All other
    # errors, including altered content and missing signatures, remain fatal.
    $isExpectedTestRoot = $SigningPolicy -eq 'test-signing' -and $result -eq 0x800B0109u
    if ($result -ne 0 -and -not $isExpectedTestRoot) {
        throw ('Authenticode verification failed (0x{0:X8}): {1}' -f $result, $path)
    }

    Write-Host "Verified $fileName with $signerName ($SigningPolicy)."
    $verifiedCount++
}
if ($verifiedCount -eq 0) {
    throw 'No signed files were verified.'
}
