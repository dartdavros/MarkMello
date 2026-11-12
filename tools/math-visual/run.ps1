#Requires -Version 5.1
<#
.SYNOPSIS
    One-shot math visual check: capture frames and print the review prompt.

.DESCRIPTION
    Runs capture.ps1, then prints every produced frame path plus the review
    template, ready to paste into a vision-capable model.

.PARAMETER Document
    Path to the .md file to open.

.PARAMETER Pages
    Number of pages to capture while scrolling the document.

.PARAMETER Slices
    Split each captured window into N horizontal bands with overlap.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Document,

    [int]$Pages = 1,

    [int]$Slices = 1
)

$ErrorActionPreference = 'Stop'

$capture = Join-Path $PSScriptRoot 'capture.ps1'
& $capture -Document $Document -Slices $Slices -Pages $Pages

$outDir = Join-Path $PSScriptRoot 'out'
$frames = Get-ChildItem -Path $outDir -Filter '*.png' | Sort-Object Name
$review = Get-Content -Path (Join-Path $PSScriptRoot 'review.md') -Raw

Write-Output ''
Write-Output '===== IMAGES ====='
foreach ($frame in $frames) {
    Write-Output ('- ' + $frame.FullName)
}
Write-Output ''
Write-Output '===== PROMPT ====='
Write-Output $review
