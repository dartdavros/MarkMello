@echo off
setlocal EnableExtensions
chcp 65001 >nul

REM Архивирует текущую папку с сохранением структуры.
REM Исключает только то, что задано в .gitignore.
REM Требует установленный git и запуск из корня git-репозитория.

echo.
echo === Packaging current folder using .gitignore rules... Please wait ===
echo.

where git >nul 2>nul
if errorlevel 1 (
  echo !!! ERROR: git not found in PATH
  echo Install Git and try again.
  echo.
  echo Press any key to close...
  pause >nul
  exit /b 1
)

git rev-parse --show-toplevel >nul 2>nul
if errorlevel 1 (
  echo !!! ERROR: current folder is not inside a git repository
  echo Run this script from the repository root.
  echo.
  echo Press any key to close...
  pause >nul
  exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "& { " ^
  "  $ErrorActionPreference = 'Stop'; " ^
  "  Add-Type -AssemblyName System.IO.Compression; " ^
  "  Add-Type -AssemblyName System.IO.Compression.FileSystem; " ^
  "" ^
  "  $root = (git rev-parse --show-toplevel).Trim(); " ^
  "  if([string]::IsNullOrWhiteSpace($root)){ throw 'Не удалось определить корень git-репозитория.' } " ^
  "  Set-Location -LiteralPath $root; " ^
  "" ^
  "  $folderName = Split-Path -Leaf $root; " ^
  "  if([string]::IsNullOrWhiteSpace($folderName)){ $folderName = 'archive' } " ^
  "  $dest = Join-Path $root ($folderName + '.zip'); " ^
  "" ^
  "  $gitArgs = @(" ^
  "    'ls-files'," ^
  "    '--cached'," ^
  "    '--others'," ^
  "    '--exclude-per-directory=.gitignore'," ^
  "    '-z'" ^
  "  ); " ^
  "" ^
  "  $raw = & git @gitArgs; " ^
  "  if($LASTEXITCODE -ne 0){ throw 'git ls-files завершился с ошибкой.' } " ^
  "" ^
  "  $files = @(); " ^
  "  if($raw){ " ^
  "    $files = ($raw -split [char]0) | Where-Object { $_ -and -not [string]::IsNullOrWhiteSpace($_) }; " ^
  "  } " ^
  "" ^
  "  $files = $files | Where-Object { (Join-Path $root $_) -ne $dest }; " ^
  "" ^
  "  if(-not $files -or $files.Count -eq 0){ throw 'Нет файлов для архивации после применения .gitignore.' } " ^
  "" ^
  "  if(Test-Path -LiteralPath $dest){ Remove-Item -LiteralPath $dest -Force } " ^
  "" ^
  "  $zip = [System.IO.Compression.ZipFile]::Open($dest, [System.IO.Compression.ZipArchiveMode]::Create); " ^
  "  try { " ^
  "    foreach($rel in $files){ " ^
  "      $fullPath = Join-Path $root $rel; " ^
  "      if(-not (Test-Path -LiteralPath $fullPath -PathType Leaf)){ continue } " ^
  "      $entryName = ($rel -replace '\\','/'); " ^
  "      [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(" ^
  "        $zip, $fullPath, $entryName, [System.IO.Compression.CompressionLevel]::Optimal" ^
  "      ) | Out-Null; " ^
  "    } " ^
  "  } finally { " ^
  "    $zip.Dispose(); " ^
  "  } " ^
  "" ^
  "  Write-Host ('Created: ' + $dest); " ^
  "} "

set "RC=%ERRORLEVEL%"
echo.
if not "%RC%"=="0" (
  echo !!! ERROR: powershell exit code %RC%
  echo (Scroll up to see the PowerShell error text.)
) else (
  echo Done.
)

echo.
echo Press any key to close...
pause >nul

endlocal
exit /b %RC%
