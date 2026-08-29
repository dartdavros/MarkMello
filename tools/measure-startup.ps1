<#
.SYNOPSIS
    Снимает тайминги старта MarkMello в single-file сценарии.

.DESCRIPTION
    Запускает приложение в smoke-режиме (--smoke-exit-after-open), где оно печатает
    снимок IStartupMetrics и выходит само. Считает медиану, минимум и максимум по стадиям
    плюс пиковое рабочее множество процесса.

    Опорные значения и методика — docs/implementation-plan-folders-tabs.md,
    раздел «Зафиксированные находки M0». Мерить только на незанятой машине:
    параллельная сборка искажает результат примерно на 15 %.

.EXAMPLE
    # Продуктовая AOT-сборка (для приёмки этапа)
    dotnet publish .\src\MarkMello.Desktop\MarkMello.Desktop.csproj -m:1 -c Release -r win-x64 `
      --self-contained true -p:PublishAot=true -p:PublishSingleFile=false -o .\publish\m0-baseline-win-x64
    .\tools\measure-startup.ps1 -Exe .\publish\m0-baseline-win-x64\MarkMello.exe -Label "AOT"

.EXAMPLE
    # Быстрая проверка между этапами
    dotnet build .\src\MarkMello.Desktop\MarkMello.Desktop.csproj -c Release
    .\tools\measure-startup.ps1

.NOTES
    На Windows AOT-публикация требует vswhere.exe в PATH:
    $env:PATH = "C:\Program Files (x86)\Microsoft Visual Studio\Installer;" + $env:PATH
#>
param(
    [string]$Exe = ".\src\MarkMello.Desktop\bin\Release\net10.0\MarkMello.exe",
    [string]$Document = ".\sample.md",
    [string]$Label = "Release (JIT)",
    [int]$Warmups = 2,
    [int]$Runs = 10
)

$ErrorActionPreference = 'Stop'

$exePath = (Resolve-Path $Exe).Path
$docPath = (Resolve-Path $Document).Path
$workingDirectory = Split-Path -Parent $docPath

function Invoke-StartupRun {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $exePath
    $psi.Arguments = "--smoke-exit-after-open `"$docPath`""
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.WorkingDirectory = $workingDirectory

    $process = [System.Diagnostics.Process]::Start($psi)

    # Рабочее множество читаем на живом процессе: после выхода PeakWorkingSet64 возвращает 0.
    $peak = 0L
    $stdout = $process.StandardOutput.ReadToEndAsync()
    while (-not $process.HasExited) {
        try {
            $process.Refresh()
            if ($process.WorkingSet64 -gt $peak) { $peak = $process.WorkingSet64 }
        } catch { }
        Start-Sleep -Milliseconds 50
    }

    $output = $stdout.GetAwaiter().GetResult()
    $process.WaitForExit()

    $result = @{ ExitCode = $process.ExitCode; WorkingSetMb = [math]::Round($peak / 1MB, 1) }
    foreach ($line in ($output -split "`n")) {
        if ($line -match '^\[startup\]\s+(\w+)\s+([\d.,]+)\s+ms') {
            $result[$matches[1]] = [double](($matches[2]) -replace ',', '.')
        }
    }

    $process.Dispose()
    return $result
}

function Get-Stat($values) {
    $sorted = @($values | Where-Object { $_ -ne $null } | Sort-Object)
    if ($sorted.Count -eq 0) { return "n/a" }
    $median = if ($sorted.Count % 2 -eq 1) {
        $sorted[[int](($sorted.Count - 1) / 2)]
    } else {
        ($sorted[$sorted.Count / 2 - 1] + $sorted[$sorted.Count / 2]) / 2
    }
    return ("median {0} | min {1} | max {2}" -f [math]::Round($median, 1), [math]::Round($sorted[0], 1), [math]::Round($sorted[-1], 1))
}

Write-Output "=== $Label ==="
Write-Output "exe: $exePath"
Write-Output "doc: $docPath"

for ($i = 0; $i -lt $Warmups; $i++) { [void](Invoke-StartupRun) }

$runs = @()
for ($i = 1; $i -le $Runs; $i++) {
    $run = Invoke-StartupRun
    $runs += $run
    Write-Output ("run {0,2}: exit={1} FirstWindow={2} DocModel={3} Readable={4} WS={5} MB" -f `
        $i, $run.ExitCode, $run.FirstWindow, $run.DocumentModelReady, $run.ReadableDocument, $run.WorkingSetMb)
}

Write-Output ""
Write-Output "--- ИТОГ: $Label, $Runs прогонов (мс от старта процесса) ---"
foreach ($stage in @('AppBootstrap', 'FirstWindow', 'DocumentModelReady', 'ReadableDocument', 'SecondaryFeatures')) {
    Write-Output ("{0,-20} {1}" -f $stage, (Get-Stat ($runs | ForEach-Object { $_[$stage] })))
}
Write-Output ("{0,-20} {1}" -f 'WorkingSet, MB', (Get-Stat ($runs | ForEach-Object { $_.WorkingSetMb })))
Write-Output ("exit codes: {0}" -f (($runs | ForEach-Object { $_.ExitCode }) -join ','))
