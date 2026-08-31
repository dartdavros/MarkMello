# Замер folder mode с чистой сессией: сохранённая сессия восстанавливает вкладки
# и делает раскрытие узла бесплатным, из-за чего цифры перестают быть сравнимыми.
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [Parameter(Mandatory = $true)][string]$Folder,
    [string]$Label = "folder",
    [int]$Runs = 5
)

$ErrorActionPreference = 'Stop'
$settings = Join-Path $env:APPDATA "MarkMello\settings.json"
$script = Join-Path $PSScriptRoot "measure-startup.ps1"

function Clear-Session {
    if (-not (Test-Path $settings)) { return }
    $json = Get-Content $settings -Raw | ConvertFrom-Json
    if ($json.PSObject.Properties.Name -contains 'session') {
        $json.session = $null
        $json | ConvertTo-Json -Depth 10 | Set-Content $settings -Encoding UTF8
    }
}

$openFolder = @()
$expandNode = @()
$workingSet = @()

for ($i = 1; $i -le $Runs; $i++) {
    Clear-Session
    $output = & $script -Exe $Exe -Folder $Folder -Label "$Label #$i" -Runs 1 -Warmups 0 2>&1 | Out-String

    if ($output -match 'OpenFolder=([\d.,]+)') { $openFolder += [double](($matches[1]) -replace ',', '.') }
    if ($output -match 'ExpandNode=([\d.,]+)') { $expandNode += [double](($matches[1]) -replace ',', '.') }
    if ($output -match 'WS=([\d.,]+) MB') { $workingSet += [double](($matches[1]) -replace ',', '.') }
}

function Show($name, $values) {
    if ($values.Count -eq 0) { Write-Output "$name : n/a"; return }
    $sorted = $values | Sort-Object
    $median = $sorted[[int](($sorted.Count - 1) / 2)]
    Write-Output ("{0,-18} median {1} | min {2} | max {3}" -f $name, [math]::Round($median, 1), [math]::Round($sorted[0], 1), [math]::Round($sorted[-1], 1))
}

Write-Output ""
Write-Output "--- $Label (чистая сессия, $Runs холодных прогонов) ---"
Show "OpenFolder" $openFolder
Show "ExpandNode" $expandNode
Show "WorkingSet, MB" $workingSet
