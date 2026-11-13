#Requires -Version 5.1
<#
.SYNOPSIS
    Prints which window is actually in the foreground and what MarkMello reports.

.DESCRIPTION
    Diagnostic helper for tools/math-visual. The screen grab copies whatever is
    on top of the window rect, so if MarkMello is not truly foreground the frames
    contain a foreign window. Run this when frames look wrong.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class MmDiagNative {
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
}
"@

$fg = [MmDiagNative]::GetForegroundWindow()
$sb = New-Object System.Text.StringBuilder 512
[void][MmDiagNative]::GetWindowText($fg, $sb, 512)
$fgPid = 0
[void][MmDiagNative]::GetWindowThreadProcessId($fg, [ref]$fgPid)

$fgName = '?'
try { $fgName = (Get-Process -Id $fgPid -ErrorAction Stop).ProcessName } catch { }

Write-Output ("FOREGROUND pid={0} proc={1} title='{2}'" -f $fgPid, $fgName, $sb.ToString())
Write-Output ('MARKMELLO_FOREGROUND ' + $(if ($fgName -eq 'MarkMello') { 'yes' } else { 'NO  <-- frames will show the wrong window' }))

foreach ($p in (Get-Process -Name 'MarkMello' -ErrorAction SilentlyContinue)) {
    Write-Output ("MM pid={0} hwnd={1} title='{2}' responding={3}" -f $p.Id, $p.MainWindowHandle, $p.MainWindowTitle, $p.Responding)
}
