param([string]$Version = '0.2.1')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$publish = Join-Path $artifacts 'publish'
$package = Join-Path $artifacts 'package'

if (Test-Path -LiteralPath $artifacts) { Remove-Item -LiteralPath $artifacts -Recurse -Force }
New-Item -ItemType Directory -Path $publish,$package -Force | Out-Null

dotnet publish (Join-Path $root 'src\CodexHpBar\CodexHpBar.csproj') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false `
    -p:Version=$Version -o $publish

$sourceExe = Join-Path $publish 'CodexHpBar.exe'
$releaseExe = Join-Path $artifacts "CodexHpBar-v$Version-win-x64.exe"
Copy-Item -LiteralPath $sourceExe -Destination $releaseExe
Copy-Item -LiteralPath $sourceExe -Destination (Join-Path $package 'CodexHpBar.exe')
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $package
Copy-Item -LiteralPath (Join-Path $root 'install.ps1') -Destination $package
Copy-Item -LiteralPath (Join-Path $root 'uninstall.ps1') -Destination $package
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination (Join-Path $package '使用說明.md')

$zip = Join-Path $artifacts "CodexHpBar-v$Version-win-x64-portable.zip"
Compress-Archive -Path (Join-Path $package '*') -DestinationPath $zip -CompressionLevel Optimal
$checksums = @($releaseExe,$zip,(Join-Path $root 'install.ps1'),(Join-Path $root 'uninstall.ps1')) | ForEach-Object {
    $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256
    "$($hash.Hash.ToLowerInvariant())  $(Split-Path -Leaf $_)"
}
[IO.File]::WriteAllLines((Join-Path $artifacts 'SHA256SUMS.txt'), $checksums, [Text.UTF8Encoding]::new($false))
Write-Host "Release 成品已建立於 $artifacts"
