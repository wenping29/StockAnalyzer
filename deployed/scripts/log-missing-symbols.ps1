[CmdletBinding()]
param(
    [string]$RepoRoot = '',
    [string]$ReportPath = ''
)

$ErrorActionPreference = 'Stop'
$scriptDir = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = [System.IO.Path]::GetFullPath((Join-Path -Path $scriptDir -ChildPath '..\..'))
}
if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path -Path $scriptDir -ChildPath 'missing-symbols-report.md'
}
$RepoRoot = [System.IO.Path]::GetFullPath($RepoRoot)

$primarySymbols = @('too_few_records', 'not_authorized', 'too_many_requests', 'remote_fail', 'memory_error')
$extraSymbols = @('stale', 'partial', 'MissingSymbols', 'missing-symbols', 'missing_symbols')
$allSymbols = $primarySymbols + $extraSymbols
$regex = '\b(too_few_records|not_authorized|too_many_requests|remote_fail|memory_error|stale|partial|MissingSymbols|missing-symbols|missing_symbols)\b'

$includeExt = @('.py', '.cs', '.ps1', '.md')
$reportFullPath = [System.IO.Path]::GetFullPath($ReportPath)

$files = @(Get-ChildItem -LiteralPath $RepoRoot -Recurse -File -Force |
    Where-Object { $includeExt -contains $_.Extension.ToLowerInvariant() } |
    Where-Object { $_.FullName -notmatch '\\(\.git|node_modules|bin|obj)(\\|$)' } |
    Where-Object { $_.FullName -ne $reportFullPath } |
    Sort-Object FullName)

$hitsPerSymbol = @{}
foreach ($symbol in $allSymbols) { $hitsPerSymbol[$symbol] = @() }

$filesScanned = 0
$filesWithHits = @()

foreach ($file in $files) {
    try {
        $matches = Select-String -LiteralPath $file.FullName -Pattern $regex -AllMatches
    }
    catch {
        continue
    }
    if ($null -eq $matches -or $matches.Count -eq 0) { continue }
    $filesScanned++
    foreach ($match in $matches) {
        foreach ($m in $match.Matches) {
            $symbolKey = $null
            foreach ($symbol in $allSymbols) {
                if ($m.Value -ieq $symbol) { $symbolKey = $symbol; break }
            }
            if ($null -eq $symbolKey) { continue }
            $relative = $file.FullName.Substring($RepoRoot.Length).TrimStart('\')
            $hitsPerSymbol[$symbolKey] += [PSCustomObject]@{
                Symbol   = $symbolKey
                File     = $relative
                Line     = $match.LineNumber
                Text     = $match.Line.Trim()
            }
        }
    }
}

$filesWithHits = @($hitsPerSymbol.Values | ForEach-Object { $_ } | ForEach-Object { $_.File } | Sort-Object -Unique)
$primaryHitCount = @($primarySymbols | ForEach-Object { $hitsPerSymbol[$_].Count } | Measure-Object -Sum).Sum
$extraHitFiles = @(@('stale', 'partial') | ForEach-Object { $hitsPerSymbol[$_] } | ForEach-Object { $_.File } | Sort-Object -Unique)

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('# Missing Symbols Trace Report')
$lines.Add('')
$lines.Add(('- Generated: {0}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')))
$lines.Add(('- RepoRoot: ' + $RepoRoot))
$lines.Add(('- Scanned extensions: ' + ($includeExt -join ', ')))
$lines.Add(('- Excluded directories: .git, node_modules, bin, obj'))
$lines.Add(('- Files scanned: ' + $files.Count))
$lines.Add(('- Files with any hit: ' + $filesWithHits.Count))
$lines.Add(('- Method: Select-String, case-insensitive, word-bounded regex over the symbol whitelist; diagnostic only, no code changes.'))
$lines.Add('')
$lines.Add('## Primary checklist symbols (missing-symbols backlog)')
$lines.Add('')
$lines.Add('| Symbol | Hits | Files |')
$lines.Add('| --- | ---: | --- |')
foreach ($symbol in $primarySymbols) {
    $hitList = @($hitsPerSymbol[$symbol])
    $fileList = @($hitList | ForEach-Object { $_.File } | Sort-Object -Unique)
    $lines.Add(('| {0} | {1} | {2} |' -f $symbol, $hitList.Count, $(if ($fileList.Count) { $fileList -join '<br>' } else { '(none)' })))
}
$lines.Add('')
foreach ($symbol in $primarySymbols) {
    $hitList = @($hitsPerSymbol[$symbol])
    if ($hitList.Count -eq 0) { continue }
    $lines.Add(('### {0}' -f $symbol))
    $lines.Add('')
    $lines.Add('| File : Line | Matched text |')
    $lines.Add('| --- | --- |')
    foreach ($hit in $hitList) {
        $lines.Add(('| {0} : {1} | {2} |' -f $hit.File, $hit.Line, $hit.Text))
    }
    $lines.Add('')
}
$lines.Add('## Additional symbols checked (non-backlog context)')
$lines.Add('')
$lines.Add('| Symbol | Hits | Files |')
$lines.Add('| --- | ---: | --- |')
foreach ($symbol in $extraSymbols) {
    $hitList = @($hitsPerSymbol[$symbol])
    $fileList = @($hitList | ForEach-Object { $_.File } | Sort-Object -Unique)
    $lines.Add(('| {0} | {1} | {2} |' -f $symbol, $hitList.Count, $(if ($fileList.Count) { $fileList -join '<br>' } else { '(none)' })))
}
$lines.Add('')
foreach ($symbol in $extraSymbols) {
    $hitList = @($hitsPerSymbol[$symbol])
    if ($hitList.Count -eq 0) { continue }
    $lines.Add(('### {0}' -f $symbol))
    $lines.Add('')
    $lines.Add('| File : Line | Matched text |')
    $lines.Add('| --- | --- |')
    foreach ($hit in $hitList) {
        $lines.Add(('| {0} : {1} | {2} |' -f $hit.File, $hit.Line, $hit.Text))
    }
    $lines.Add('')
}
$lines.Add('## Conclusions for this scan')
$lines.Add('')
if ($primaryHitCount -eq 0) {
    $lines.Add('- No primary checklist symbol (`too_few_records`, `not_authorized`, `too_many_requests`, `remote_fail`, `memory_error`) appears anywhere in the scanned local repository. The missing-symbols list has no traceable origin in this repo.')
} else {
    $lines.Add(('- Primary symbols found locally: {0} hit(s); see tables above.' -f $primaryHitCount))
}
$lines.Add('- `stale` and `partial` hits are recorded as non-target semantics until confirmed otherwise:')
$lines.Add('  - `stale`: previously observed only in FundAnalysis.Core/Services/FundEstimateService.cs (lines 46/49/50) as a local variable refreshing stale fund holdings, not missing-symbol error semantics.')
$lines.Add('  - `partial`: previously observed only under FundAnalysis.Core/Migrations/ as the C# `partial` modifier, not missing-symbol error semantics.')
if ($extraHitFiles.Count -gt 0) {
    $lines.Add(('- Files containing `stale`/`partial` hits this run: ' + ($extraHitFiles -join ', ')))
}
$lines.Add('- Symbols `remote_fail`, `memory_error`, `too_few_records`, `not_authorized`, `too_many_requests` remain unattributed; do not extend the checklist or invent new symbols without user confirmation.')

$reportText = ($lines -join "`r`n") + "`r`n"
$encoding = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($reportFullPath, $reportText, $encoding)

Write-Output ('RepoRoot: ' + $RepoRoot)
Write-Output ('Files scanned: ' + $files.Count)
Write-Output ('Files with hits: ' + $filesWithHits.Count)
foreach ($symbol in $allSymbols) {
    Write-Output ('{0}: {1} hit(s)' -f $symbol, @($hitsPerSymbol[$symbol]).Count)
}
Write-Output ('Report: ' + $reportFullPath)
