# Missing Symbols Trace Report

- Generated: 2026-11-06 13:30:04
- RepoRoot: D:\Project\StockAnalyzer
- Scanned extensions: .py, .cs, .ps1, .md
- Excluded directories: .git, node_modules, bin, obj
- Files scanned: 54
- Files with any hit: 13
- Method: Select-String, case-insensitive, word-bounded regex over the symbol whitelist; diagnostic only, no code changes.

## Primary checklist symbols (missing-symbols backlog)

| Symbol | Hits | Files |
| --- | ---: | --- |
| too_few_records | 4 | deployed\scripts\log-missing-symbols.ps1 |
| not_authorized | 4 | deployed\scripts\log-missing-symbols.ps1 |
| too_many_requests | 4 | deployed\scripts\log-missing-symbols.ps1 |
| remote_fail | 4 | deployed\scripts\log-missing-symbols.ps1 |
| memory_error | 4 | deployed\scripts\log-missing-symbols.ps1 |

### too_few_records

| File : Line | Matched text |
| --- | --- |
| deployed\scripts\log-missing-symbols.ps1 : 17 | $primarySymbols = @('too_few_records', 'not_authorized', 'too_many_requests', 'remote_fail', 'memory_error') |
| deployed\scripts\log-missing-symbols.ps1 : 20 | $regex = '\b(too_few_records|not_authorized|too_many_requests|remote_fail|memory_error|stale|partial|MissingSymbols|missing-symbols|missing_symbols)\b' |
| deployed\scripts\log-missing-symbols.ps1 : 126 | $lines.Add('- No primary checklist symbol (`too_few_records`, `not_authorized`, `too_many_requests`, `remote_fail`, `memory_error`) appears anywhere in the scanned local repository. The missing-symbols list has no traceable origin in this repo.') |
| deployed\scripts\log-missing-symbols.ps1 : 136 | $lines.Add('- Symbols `remote_fail`, `memory_error`, `too_few_records`, `not_authorized`, `too_many_requests` remain unattributed; do not extend the checklist or invent new symbols without user confirmation.') |

### not_authorized

| File : Line | Matched text |
| --- | --- |
| deployed\scripts\log-missing-symbols.ps1 : 17 | $primarySymbols = @('too_few_records', 'not_authorized', 'too_many_requests', 'remote_fail', 'memory_error') |
| deployed\scripts\log-missing-symbols.ps1 : 20 | $regex = '\b(too_few_records|not_authorized|too_many_requests|remote_fail|memory_error|stale|partial|MissingSymbols|missing-symbols|missing_symbols)\b' |
| deployed\scripts\log-missing-symbols.ps1 : 126 | $lines.Add('- No primary checklist symbol (`too_few_records`, `not_authorized`, `too_many_requests`, `remote_fail`, `memory_error`) appears anywhere in the scanned local repository. The missing-symbols list has no traceable origin in this repo.') |
| deployed\scripts\log-missing-symbols.ps1 : 136 | $lines.Add('- Symbols `remote_fail`, `memory_error`, `too_few_records`, `not_authorized`, `too_many_requests` remain unattributed; do not extend the checklist or invent new symbols without user confirmation.') |

### too_many_requests

| File : Line | Matched text |
| --- | --- |
| deployed\scripts\log-missing-symbols.ps1 : 17 | $primarySymbols = @('too_few_records', 'not_authorized', 'too_many_requests', 'remote_fail', 'memory_error') |
| deployed\scripts\log-missing-symbols.ps1 : 20 | $regex = '\b(too_few_records|not_authorized|too_many_requests|remote_fail|memory_error|stale|partial|MissingSymbols|missing-symbols|missing_symbols)\b' |
| deployed\scripts\log-missing-symbols.ps1 : 126 | $lines.Add('- No primary checklist symbol (`too_few_records`, `not_authorized`, `too_many_requests`, `remote_fail`, `memory_error`) appears anywhere in the scanned local repository. The missing-symbols list has no traceable origin in this repo.') |
| deployed\scripts\log-missing-symbols.ps1 : 136 | $lines.Add('- Symbols `remote_fail`, `memory_error`, `too_few_records`, `not_authorized`, `too_many_requests` remain unattributed; do not extend the checklist or invent new symbols without user confirmation.') |

### remote_fail

| File : Line | Matched text |
| --- | --- |
| deployed\scripts\log-missing-symbols.ps1 : 17 | $primarySymbols = @('too_few_records', 'not_authorized', 'too_many_requests', 'remote_fail', 'memory_error') |
| deployed\scripts\log-missing-symbols.ps1 : 20 | $regex = '\b(too_few_records|not_authorized|too_many_requests|remote_fail|memory_error|stale|partial|MissingSymbols|missing-symbols|missing_symbols)\b' |
| deployed\scripts\log-missing-symbols.ps1 : 126 | $lines.Add('- No primary checklist symbol (`too_few_records`, `not_authorized`, `too_many_requests`, `remote_fail`, `memory_error`) appears anywhere in the scanned local repository. The missing-symbols list has no traceable origin in this repo.') |
| deployed\scripts\log-missing-symbols.ps1 : 136 | $lines.Add('- Symbols `remote_fail`, `memory_error`, `too_few_records`, `not_authorized`, `too_many_requests` remain unattributed; do not extend the checklist or invent new symbols without user confirmation.') |

### memory_error

| File : Line | Matched text |
| --- | --- |
| deployed\scripts\log-missing-symbols.ps1 : 17 | $primarySymbols = @('too_few_records', 'not_authorized', 'too_many_requests', 'remote_fail', 'memory_error') |
| deployed\scripts\log-missing-symbols.ps1 : 20 | $regex = '\b(too_few_records|not_authorized|too_many_requests|remote_fail|memory_error|stale|partial|MissingSymbols|missing-symbols|missing_symbols)\b' |
| deployed\scripts\log-missing-symbols.ps1 : 126 | $lines.Add('- No primary checklist symbol (`too_few_records`, `not_authorized`, `too_many_requests`, `remote_fail`, `memory_error`) appears anywhere in the scanned local repository. The missing-symbols list has no traceable origin in this repo.') |
| deployed\scripts\log-missing-symbols.ps1 : 136 | $lines.Add('- Symbols `remote_fail`, `memory_error`, `too_few_records`, `not_authorized`, `too_many_requests` remain unattributed; do not extend the checklist or invent new symbols without user confirmation.') |

## Additional symbols checked (non-backlog context)

| Symbol | Hits | Files |
| --- | ---: | --- |
| stale | 10 | backend\FundAnalysis.Core\Services\FundEstimateService.cs<br>deployed\scripts\log-missing-symbols.ps1 |
| partial | 18 | backend\FundAnalysis.Core\Migrations\20261024041644_Init.cs<br>backend\FundAnalysis.Core\Migrations\20261024041644_Init.Designer.cs<br>backend\FundAnalysis.Core\Migrations\20261024060625_AddStockBasicIndustry.cs<br>backend\FundAnalysis.Core\Migrations\20261024060625_AddStockBasicIndustry.Designer.cs<br>backend\FundAnalysis.Core\Migrations\20261024141523_AddOmoOperation.cs<br>backend\FundAnalysis.Core\Migrations\20261024141523_AddOmoOperation.Designer.cs<br>backend\FundAnalysis.Core\Migrations\20261024162833_AddMarketIndicatorAndIntraday.cs<br>backend\FundAnalysis.Core\Migrations\20261024162833_AddMarketIndicatorAndIntraday.Designer.cs<br>backend\FundAnalysis.Core\Migrations\20261106034519_AddFundHoldings.cs<br>backend\FundAnalysis.Core\Migrations\20261106034519_AddFundHoldings.Designer.cs<br>backend\FundAnalysis.Core\Migrations\AppDbContextModelSnapshot.cs<br>deployed\scripts\log-missing-symbols.ps1 |
| MissingSymbols | 2 | deployed\scripts\log-missing-symbols.ps1 |
| missing-symbols | 5 | deployed\scripts\log-missing-symbols.ps1 |
| missing_symbols | 2 | deployed\scripts\log-missing-symbols.ps1 |

### stale

| File : Line | Matched text |
| --- | --- |
| backend\FundAnalysis.Core\Services\FundEstimateService.cs : 46 | var stale = funds.Keys |
| backend\FundAnalysis.Core\Services\FundEstimateService.cs : 49 | if (stale.Count > 0) |
| backend\FundAnalysis.Core\Services\FundEstimateService.cs : 50 | await RefreshHoldingsAsync(stale, ct); |
| deployed\scripts\log-missing-symbols.ps1 : 18 | $extraSymbols = @('stale', 'partial', 'MissingSymbols', 'missing-symbols', 'missing_symbols') |
| deployed\scripts\log-missing-symbols.ps1 : 20 | $regex = '\b(too_few_records|not_authorized|too_many_requests|remote_fail|memory_error|stale|partial|MissingSymbols|missing-symbols|missing_symbols)\b' |
| deployed\scripts\log-missing-symbols.ps1 : 66 | $extraHitFiles = @(@('stale', 'partial') | ForEach-Object { $hitsPerSymbol[$_] } | ForEach-Object { $_.File } | Sort-Object -Unique) |
| deployed\scripts\log-missing-symbols.ps1 : 130 | $lines.Add('- `stale` and `partial` hits are recorded as non-target semantics until confirmed otherwise:') |
| deployed\scripts\log-missing-symbols.ps1 : 131 | $lines.Add('  - `stale`: previously observed only in FundAnalysis.Core/Services/FundEstimateService.cs (lines 46/49/50) as a local variable refreshing stale fund holdings, not missing-symbol error semantics.') |
| deployed\scripts\log-missing-symbols.ps1 : 131 | $lines.Add('  - `stale`: previously observed only in FundAnalysis.Core/Services/FundEstimateService.cs (lines 46/49/50) as a local variable refreshing stale fund holdings, not missing-symbol error semantics.') |
| deployed\scripts\log-missing-symbols.ps1 : 134 | $lines.Add(('- Files containing `stale`/`partial` hits this run: ' + ($extraHitFiles -join ', '))) |

### partial

| File : Line | Matched text |
| --- | --- |
| backend\FundAnalysis.Core\Migrations\20261024041644_Init.cs : 10 | public partial class Init : Migration |
| backend\FundAnalysis.Core\Migrations\20261024041644_Init.Designer.cs : 16 | partial class Init |
| backend\FundAnalysis.Core\Migrations\20261024060625_AddStockBasicIndustry.cs : 8 | public partial class AddStockBasicIndustry : Migration |
| backend\FundAnalysis.Core\Migrations\20261024060625_AddStockBasicIndustry.Designer.cs : 16 | partial class AddStockBasicIndustry |
| backend\FundAnalysis.Core\Migrations\20261024141523_AddOmoOperation.cs : 10 | public partial class AddOmoOperation : Migration |
| backend\FundAnalysis.Core\Migrations\20261024141523_AddOmoOperation.Designer.cs : 16 | partial class AddOmoOperation |
| backend\FundAnalysis.Core\Migrations\20261024162833_AddMarketIndicatorAndIntraday.cs : 10 | public partial class AddMarketIndicatorAndIntraday : Migration |
| backend\FundAnalysis.Core\Migrations\20261024162833_AddMarketIndicatorAndIntraday.Designer.cs : 16 | partial class AddMarketIndicatorAndIntraday |
| backend\FundAnalysis.Core\Migrations\20261106034519_AddFundHoldings.cs : 9 | public partial class AddFundHoldings : Migration |
| backend\FundAnalysis.Core\Migrations\20261106034519_AddFundHoldings.Designer.cs : 16 | partial class AddFundHoldings |
| backend\FundAnalysis.Core\Migrations\AppDbContextModelSnapshot.cs : 14 | partial class AppDbContextModelSnapshot : ModelSnapshot |
| deployed\scripts\log-missing-symbols.ps1 : 18 | $extraSymbols = @('stale', 'partial', 'MissingSymbols', 'missing-symbols', 'missing_symbols') |
| deployed\scripts\log-missing-symbols.ps1 : 20 | $regex = '\b(too_few_records|not_authorized|too_many_requests|remote_fail|memory_error|stale|partial|MissingSymbols|missing-symbols|missing_symbols)\b' |
| deployed\scripts\log-missing-symbols.ps1 : 66 | $extraHitFiles = @(@('stale', 'partial') | ForEach-Object { $hitsPerSymbol[$_] } | ForEach-Object { $_.File } | Sort-Object -Unique) |
| deployed\scripts\log-missing-symbols.ps1 : 130 | $lines.Add('- `stale` and `partial` hits are recorded as non-target semantics until confirmed otherwise:') |
| deployed\scripts\log-missing-symbols.ps1 : 132 | $lines.Add('  - `partial`: previously observed only under FundAnalysis.Core/Migrations/ as the C# `partial` modifier, not missing-symbol error semantics.') |
| deployed\scripts\log-missing-symbols.ps1 : 132 | $lines.Add('  - `partial`: previously observed only under FundAnalysis.Core/Migrations/ as the C# `partial` modifier, not missing-symbol error semantics.') |
| deployed\scripts\log-missing-symbols.ps1 : 134 | $lines.Add(('- Files containing `stale`/`partial` hits this run: ' + ($extraHitFiles -join ', '))) |

### MissingSymbols

| File : Line | Matched text |
| --- | --- |
| deployed\scripts\log-missing-symbols.ps1 : 18 | $extraSymbols = @('stale', 'partial', 'MissingSymbols', 'missing-symbols', 'missing_symbols') |
| deployed\scripts\log-missing-symbols.ps1 : 20 | $regex = '\b(too_few_records|not_authorized|too_many_requests|remote_fail|memory_error|stale|partial|MissingSymbols|missing-symbols|missing_symbols)\b' |

### missing-symbols

| File : Line | Matched text |
| --- | --- |
| deployed\scripts\log-missing-symbols.ps1 : 13 | $ReportPath = Join-Path -Path $scriptDir -ChildPath 'missing-symbols-report.md' |
| deployed\scripts\log-missing-symbols.ps1 : 18 | $extraSymbols = @('stale', 'partial', 'MissingSymbols', 'missing-symbols', 'missing_symbols') |
| deployed\scripts\log-missing-symbols.ps1 : 20 | $regex = '\b(too_few_records|not_authorized|too_many_requests|remote_fail|memory_error|stale|partial|MissingSymbols|missing-symbols|missing_symbols)\b' |
| deployed\scripts\log-missing-symbols.ps1 : 79 | $lines.Add('## Primary checklist symbols (missing-symbols backlog)') |
| deployed\scripts\log-missing-symbols.ps1 : 126 | $lines.Add('- No primary checklist symbol (`too_few_records`, `not_authorized`, `too_many_requests`, `remote_fail`, `memory_error`) appears anywhere in the scanned local repository. The missing-symbols list has no traceable origin in this repo.') |

### missing_symbols

| File : Line | Matched text |
| --- | --- |
| deployed\scripts\log-missing-symbols.ps1 : 18 | $extraSymbols = @('stale', 'partial', 'MissingSymbols', 'missing-symbols', 'missing_symbols') |
| deployed\scripts\log-missing-symbols.ps1 : 20 | $regex = '\b(too_few_records|not_authorized|too_many_requests|remote_fail|memory_error|stale|partial|MissingSymbols|missing-symbols|missing_symbols)\b' |

## Conclusions for this scan

- Primary symbols found locally: 20 hit(s); see tables above.
- `stale` and `partial` hits are recorded as non-target semantics until confirmed otherwise:
  - `stale`: previously observed only in FundAnalysis.Core/Services/FundEstimateService.cs (lines 46/49/50) as a local variable refreshing stale fund holdings, not missing-symbol error semantics.
  - `partial`: previously observed only under FundAnalysis.Core/Migrations/ as the C# `partial` modifier, not missing-symbol error semantics.
- Files containing `stale`/`partial` hits this run: backend\FundAnalysis.Core\Migrations\20261024041644_Init.cs, backend\FundAnalysis.Core\Migrations\20261024041644_Init.Designer.cs, backend\FundAnalysis.Core\Migrations\20261024060625_AddStockBasicIndustry.cs, backend\FundAnalysis.Core\Migrations\20261024060625_AddStockBasicIndustry.Designer.cs, backend\FundAnalysis.Core\Migrations\20261024141523_AddOmoOperation.cs, backend\FundAnalysis.Core\Migrations\20261024141523_AddOmoOperation.Designer.cs, backend\FundAnalysis.Core\Migrations\20261024162833_AddMarketIndicatorAndIntraday.cs, backend\FundAnalysis.Core\Migrations\20261024162833_AddMarketIndicatorAndIntraday.Designer.cs, backend\FundAnalysis.Core\Migrations\20261106034519_AddFundHoldings.cs, backend\FundAnalysis.Core\Migrations\20261106034519_AddFundHoldings.Designer.cs, backend\FundAnalysis.Core\Migrations\AppDbContextModelSnapshot.cs, backend\FundAnalysis.Core\Services\FundEstimateService.cs, deployed\scripts\log-missing-symbols.ps1
- Symbols `remote_fail`, `memory_error`, `too_few_records`, `not_authorized`, `too_many_requests` remain unattributed; do not extend the checklist or invent new symbols without user confirmation.
