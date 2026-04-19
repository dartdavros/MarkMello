param(
    [Parameter(Mandatory = $true)]
    [string[]]$Paths,

    [Parameter(Mandatory = $true)]
    [string]$CertificateBase64,

    [Parameter(Mandatory = $true)]
    [string]$CertificatePassword,

    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"

function Resolve-SignToolPath {
    $direct = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($direct) {
        return $direct.Source
    }

    $roots = @(
        (Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"),
        (Join-Path $env:ProgramFiles "Windows Kits\10\bin")
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Container) }

    $candidates = foreach ($root in $roots) {
        Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending |
            ForEach-Object {
                $candidate = Join-Path $_.FullName "x64\signtool.exe"
                if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                    $candidate
                }
            }
    }

    return $candidates | Select-Object -First 1
}

$signToolPath = Resolve-SignToolPath
if (-not $signToolPath) {
    throw "signtool.exe was not found. Install the Windows SDK or run this script on a GitHub-hosted Windows runner."
}

$resolvedPaths = $Paths |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    ForEach-Object { [System.IO.Path]::GetFullPath($_) }

if (-not $resolvedPaths -or $resolvedPaths.Count -eq 0) {
    throw "No files were provided for signing."
}

$certificateBytes = [Convert]::FromBase64String($CertificateBase64)
$certificatePath = Join-Path $env:TEMP ("markmello-signing-" + [Guid]::NewGuid().ToString("N") + ".pfx")

try {
    [System.IO.File]::WriteAllBytes($certificatePath, $certificateBytes)

    foreach ($path in $resolvedPaths) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Signing target does not exist: $path"
        }

        & $signToolPath sign `
            /fd SHA256 `
            /td SHA256 `
            /tr $TimestampUrl `
            /f $certificatePath `
            /p $CertificatePassword `
            /a `
            $path

        if ($LASTEXITCODE -ne 0) {
            throw "signtool.exe failed for $path"
        }
    }
}
finally {
    if (Test-Path -LiteralPath $certificatePath -PathType Leaf) {
        Remove-Item -LiteralPath $certificatePath -Force
    }
}
