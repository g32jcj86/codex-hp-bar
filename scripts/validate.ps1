$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet format CodexHpBar.slnx --verify-no-changes
    dotnet build CodexHpBar.slnx -c Release --no-restore
    dotnet test tests\CodexHpBar.Tests\CodexHpBar.Tests.csproj -c Release --no-build `
        /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:Threshold=85 /p:ThresholdType=line /p:ThresholdStat=total
    $vulnerabilityReport = Join-Path $env:TEMP 'codex-hp-bar-vulnerabilities.json'
    dotnet list src\CodexHpBar\CodexHpBar.csproj package --vulnerable --include-transitive --format json | Out-File $vulnerabilityReport -Encoding utf8
    $vulnerabilityJson = Get-Content -LiteralPath $vulnerabilityReport -Raw -Encoding utf8
    if ($vulnerabilityJson -match '"vulnerabilities"\s*:\s*\[\s*\{') {
        throw '相依套件包含已知弱點。'
    }
    & "$PSScriptRoot\validate-docs.ps1"
}
finally { Pop-Location }
