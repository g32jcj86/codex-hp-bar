param(
    [string]$InstallRoot = "$env:LOCALAPPDATA\Programs\CodexHpBar",
    [string]$SourceExe = "$PSScriptRoot\CodexHpBar.exe"
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $SourceExe)) {
    throw "Executable not found: $SourceExe"
}

New-Item -ItemType Directory -Path $InstallRoot -Force | Out-Null
Copy-Item -LiteralPath $SourceExe -Destination (Join-Path $InstallRoot 'CodexHpBar.exe') -Force
Write-Host "Copied to $InstallRoot. Run CodexHpBar.exe to finish first-run setup."
