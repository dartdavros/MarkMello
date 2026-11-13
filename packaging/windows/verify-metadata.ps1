param(
    [Parameter(Mandatory = $true)][string]$Directory,
    [Parameter(Mandatory = $true)][string[]]$FileNames,
    [string[]]$OptionalFileNames = @(),
    [Parameter(Mandatory = $true)][string]$ProductVersion,
    [Parameter(Mandatory = $true)][string]$FileVersion
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $Directory).Path
foreach ($name in $FileNames) {
    $path = [IO.Path]::GetFullPath((Join-Path $root $name))
    if (-not $path.StartsWith($root.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Metadata file is outside the artifact directory: $name"
    }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        if ($name -in $OptionalFileNames) { continue }
        throw "Required signing file is missing: $name"
    }
    $info = [Diagnostics.FileVersionInfo]::GetVersionInfo($path)
    if ($info.ProductName -cne 'MarkMello') {
        throw "Unexpected ProductName for ${name}: '$($info.ProductName)'"
    }
    if ($info.ProductVersion -cne $ProductVersion) {
        throw "Unexpected ProductVersion for ${name}: '$($info.ProductVersion)', expected '$ProductVersion'"
    }
    if ($info.FileVersion -cne $FileVersion) {
        throw "Unexpected FileVersion for ${name}: '$($info.FileVersion)', expected '$FileVersion'"
    }
    Write-Host "Verified metadata: $name ($ProductVersion / $FileVersion)"
}
