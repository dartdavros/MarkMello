#Requires -Version 5.1
<#
.SYNOPSIS
    Captures the MarkMello window, optionally scrolling through the document.

.DESCRIPTION
    Opens the published MarkMello.exe with the given document, brings the window
    to the foreground and copies the window region (not the whole screen) into
    out/. Because only the window region is copied, other windows cannot leak
    into the frame.

    With -ScrollStep the tool additionally sends Page Down and captures one frame
    per page, so a whole document can be reviewed formula by formula.

.PARAMETER Document
    Path to the .md file to open.

.PARAMETER Slices
    Split the captured window vertically into N horizontal bands.

.PARAMETER Overlap
    Vertical overlap (pixels) between neighbouring bands so a tall formula is
    never cut by a band boundary. Defaults to 80.

.PARAMETER Pages
    Number of pages to capture while scrolling. 1 = top of the document only.

.PARAMETER Exe
    Path to MarkMello.exe. Defaults to dist-exe\MarkMello.exe in the repo root.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Document,

    [int]$Slices = 1,

    [int]$Overlap = 80,

    [int]$Pages = 1,

    [string]$Exe = ""
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class MmCaptureNative {
    [DllImport("user32.dll", SetLastError = true)] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
}
"@

function Get-ForegroundWindowHandle {
    return [MmCaptureNative]::GetForegroundWindow()
}

function Minimize-ForeignWindows {
    # The screen grab copies whatever sits on top of the window rect. Windows
    # that are not MarkMello are minimized so they cannot leak into the frame;
    # this is a temporary state, not a stub — the app under test stays real.
    $shell = New-Object -ComObject Shell.Application
    $hwnd = Get-ForegroundWindowHandle
    $attempt = 0
    while ($attempt -lt 6) {
        $attempt++
        $fgPid = 0
        [void][MmCaptureNative]::GetWindowThreadProcessId($hwnd, [ref]$fgPid)
        $fgProc = $null
        try { $fgProc = (Get-Process -Id $fgPid -ErrorAction Stop).ProcessName } catch { }
        if ($fgProc -eq 'MarkMello' -or $fgPid -eq 0) { return }

        Write-Output ("MINIMIZE foreign window proc='{0}'" -f $fgProc)
        $shell.MinimizeAll()
        Start-Sleep -Milliseconds 600
        [void][MmCaptureNative]::ShowWindow($script:hwnd, 9)
        [void][MmCaptureNative]::ShowWindow($script:hwnd, 3)
        [void][MmCaptureNative]::SetForegroundWindow($script:hwnd)
        Start-Sleep -Milliseconds 600
        $hwnd = Get-ForegroundWindowHandle
    }
}

function Get-ForegroundProcessName {
    $fg = [MmCaptureNative]::GetForegroundWindow()
    $fgPid = 0
    [void][MmCaptureNative]::GetWindowThreadProcessId($fg, [ref]$fgPid)
    if ($fgPid -eq 0) { return '' }
    try { return (Get-Process -Id $fgPid -ErrorAction Stop).ProcessName } catch { return '' }
}

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if ([string]::IsNullOrWhiteSpace($Exe)) {
    $Exe = Join-Path $root 'dist-exe\MarkMello.exe'
}
if (-not (Test-Path $Exe)) {
    throw "MarkMello.exe not found at '$Exe'. Publish it first (see tools/math-visual/README.md)."
}
if (-not (Test-Path $Document)) {
    throw "Document not found: '$Document'"
}
$Document = (Resolve-Path $Document).Path

$outDir = Join-Path $PSScriptRoot 'out'
if (Test-Path $outDir) {
    # A previously reviewed frame may still be open in another process; clearing
    # is best-effort so a stale handle cannot abort the capture run.
    Get-ChildItem -Path $outDir -Filter '*.png' -ErrorAction SilentlyContinue |
        ForEach-Object { try { Remove-Item $_.FullName -Force -ErrorAction Stop } catch { } }
}
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

$proc = Get-Process -Name 'MarkMello' -ErrorAction SilentlyContinue | Select-Object -First 1

# A running instance keeps its own document: reusing it would silently capture
# the WRONG file. Restart unless it already shows exactly the requested one.
$wantedName = Split-Path -Leaf $Document
if ($null -ne $proc) {
    $proc.Refresh()
    $currentTitle = $proc.MainWindowTitle
    if ($currentTitle -notlike "*$wantedName*") {
        Write-Output ("RESTART: open document was '{0}', wanted '{1}'" -f $currentTitle, $wantedName)
        $proc | Stop-Process -Force
        Start-Sleep -Seconds 1
        $proc = $null
    }
}

if ($null -eq $proc) {
    $proc = Start-Process -FilePath $Exe -ArgumentList "`"$Document`"" -PassThru
}

$hwnd = [IntPtr]::Zero
$deadline = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 300
    $proc.Refresh()
    if ($proc.HasExited) {
        throw 'MarkMello exited before a window appeared.'
    }
    if ($proc.MainWindowHandle -ne [IntPtr]::Zero) {
        $hwnd = $proc.MainWindowHandle
        break
    }
}
if ($hwnd -eq [IntPtr]::Zero) {
    throw 'MarkMello window did not appear within 30 seconds.'
}

function Focus-Window {
    Start-Sleep -Seconds 2
    if ([MmCaptureNative]::IsIconic($hwnd)) {
        [void][MmCaptureNative]::ShowWindow($hwnd, 9)
    }
    [void][MmCaptureNative]::ShowWindow($hwnd, 3)   # maximize
    Start-Sleep -Milliseconds 700

    # SetForegroundWindow silently fails when another process owns the
    # foreground. Retry with BringWindowToTop / TOPMOST dance, then verify: the
    # frame must contain OUR window, never whatever happened to be on top.
    $attempts = 0
    while ($attempts -lt 8) {
        $attempts++
        [void][MmCaptureNative]::SetForegroundWindow($hwnd)
        Start-Sleep -Milliseconds 250
        if ((Get-ForegroundProcessName) -eq 'MarkMello') { break }
        [void][MmCaptureNative]::BringWindowToTop($hwnd)
        # HWND_TOPMOST then HWND_NOTOPMOST forces a z-order raise.
        [void][MmCaptureNative]::SetWindowPos($hwnd, [IntPtr](-1), 0, 0, 0, 0, 0x0001 -bor 0x0002 -bor 0x0040)
        Start-Sleep -Milliseconds 200
        [void][MmCaptureNative]::SetWindowPos($hwnd, [IntPtr](-2), 0, 0, 0, 0, 0x0001 -bor 0x0002 -bor 0x0040)
    }

    $foreground = Get-ForegroundProcessName
    if ($foreground -ne 'MarkMello') {
        Minimize-ForeignWindows
        [void][MmCaptureNative]::SetForegroundWindow($hwnd)
        Start-Sleep -Milliseconds 500
        $foreground = Get-ForegroundProcessName
    }
    if ($foreground -ne 'MarkMello') {
        throw "Cannot bring MarkMello to the foreground (foreground is '$foreground'). Minimize or close that window and re-run; otherwise the frames would capture a foreign window, which is not an acceptable verification."
    }
    Start-Sleep -Seconds 1
}

function Save-Slices([System.Drawing.Bitmap]$source, [string]$prefix) {
    $rect = New-Object MmCaptureNative+RECT
    if (-not [MmCaptureNative]::GetWindowRect($hwnd, [ref]$rect)) {
        throw 'GetWindowRect failed.'
    }
    $width = $source.Width
    $height = $source.Height
    if ($Slices -lt 1) { $slicesFixed = 1 } else { $slicesFixed = $Slices }
    if ($Overlap -lt 0) { $overlapFixed = 0 } else { $overlapFixed = $Overlap }
    $bandHeight = [int][Math]::Ceiling($height / $slicesFixed)

    for ($i = 0; $i -lt $slicesFixed; $i++) {
        $pad = if ($i -gt 0) { $overlapFixed } else { 0 }
        $top = [Math]::Max(0, ($i * $bandHeight) - $pad)
        $h = [Math]::Min($bandHeight + $pad, $height - $top)
        if ($h -le 0) { break }

        $rectSlice = New-Object System.Drawing.Rectangle(0, $top, $width, $h)
        $band = $source.Clone($rectSlice, $source.PixelFormat)
        $name = if ($slicesFixed -eq 1) { "$prefix.png" } else { ('{0}-{1:D2}.png' -f $prefix, ($i + 1)) }
        $path = Join-Path $outDir $name
        $band.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        $band.Dispose()
        Write-Output ("FRAME " + $path + " y=$top h=$h")
    }
}

function Capture-Window([string]$prefix) {
    $rect = New-Object MmCaptureNative+RECT
    if (-not [MmCaptureNative]::GetWindowRect($hwnd, [ref]$rect)) {
        throw 'GetWindowRect failed.'
    }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) {
        throw "Invalid window rect: ${width}x${height}"
    }
    $bmp = New-Object System.Drawing.Bitmap($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bmp)
    $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size($width, $height)))
    $graphics.Dispose()
    Save-Slices $bmp $prefix
    $bmp.Dispose()
}

Focus-Window

if ($Pages -le 1) {
    Capture-Window 'window'
}
else {
    for ($page = 1; $page -le $Pages; $page++) {
        Capture-Window ('page-{0:D2}' -f $page)
        if ($page -lt $Pages) {
            # Focus can drift between pages (a popup, an update toast); re-assert
            # it so the next frame still contains our window.
            if ((Get-ForegroundProcessName) -ne 'MarkMello') {
                [void][MmCaptureNative]::SetForegroundWindow($hwnd)
                Start-Sleep -Milliseconds 300
            }
            [System.Windows.Forms.SendKeys]::SendWait('{PGDN}')
            Start-Sleep -Milliseconds 900
        }
    }
}

Write-Output ("WINDOW pages={0} slices={1} overlap={2}" -f $Pages, $Slices, $Overlap)
