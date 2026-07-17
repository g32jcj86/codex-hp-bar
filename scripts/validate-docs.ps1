$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$missing = [Collections.Generic.List[string]]::new()
Get-ChildItem -LiteralPath $root -Filter '*.md' -Recurse | Where-Object { $_.FullName -notmatch '\\(bin|obj|artifacts)\\' } | ForEach-Object {
    $file = $_
    $content = Get-Content -LiteralPath $file.FullName -Raw
    [regex]::Matches($content, '!?(?:\[[^\]]*\])\(([^)#]+)(?:#[^)]+)?\)') | ForEach-Object {
        $target = $_.Groups[1].Value
        if ($target -notmatch '^(https?://|mailto:)' -and $target -notmatch '^[A-Za-z]:') {
            $resolved = Join-Path $file.DirectoryName ([Uri]::UnescapeDataString($target))
            if (-not (Test-Path -LiteralPath $resolved)) { $missing.Add("$($file.Name) -> $target") }
        }
    }
}
if ($missing.Count -gt 0) { throw "Markdown 連結缺少目標：`n$($missing -join "`n")" }

$simplifiedTerms = @('软件','设置','启动项','链接失效','用户界面','后台运行')
$markdownFiles = Get-ChildItem -LiteralPath $root -Filter '*.md' -File -Recurse |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|artifacts)\\' }
foreach ($term in $simplifiedTerms) {
    $hits = $markdownFiles | Select-String -SimpleMatch $term
    if ($hits) { throw "發現簡體中文用詞 '$term'：`n$($hits -join "`n")" }
}
Write-Host 'Markdown 相對連結、圖片與繁體中文用詞檢查通過。'
