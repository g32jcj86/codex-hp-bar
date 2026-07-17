param(
    [string]$InstallRoot = "$env:LOCALAPPDATA\Programs\CodexHpBar",
    [switch]$KeepSettings
)

$ErrorActionPreference = 'Stop'
Get-Process -Name CodexHpBar -ErrorAction SilentlyContinue | Stop-Process -Force
$shortcut = Join-Path ([Environment]::GetFolderPath('Startup')) 'Codex HP Bar.lnk'
if (Test-Path -LiteralPath $shortcut) { Remove-Item -LiteralPath $shortcut -Force }
if (Test-Path -LiteralPath $InstallRoot) { Remove-Item -LiteralPath $InstallRoot -Recurse -Force }
if (-not $KeepSettings) {
    $settings = Join-Path $env:LOCALAPPDATA 'CodexHpBar'
    if (Test-Path -LiteralPath $settings) { Remove-Item -LiteralPath $settings -Recurse -Force }
}
Write-Host 'Codex HP Bar was removed. Downloaded portable copies were not deleted.'
