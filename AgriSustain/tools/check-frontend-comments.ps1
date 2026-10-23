param([string]$ProjectRoot = (Join-Path $PSScriptRoot '..'))

# Audit named declarations in these small JSX modules. Inline render/event callbacks are
# documented by the enclosing component/hook; this is not a general-purpose JS parser.
$taskPattern = '(?m)^[ \t]*(?:export (?:default )?)?(?:(?:async )?function\s+(?<function>\w+)\s*\(|const\s+(?<arrow>\w+)\s*=\s*(?:async\s*)?\([^\r\n]*?\)\s*=>)'
$taskCount = 0
$taskMissing = @()
Get-ChildItem -LiteralPath (Join-Path $ProjectRoot 'frontend/src') -Recurse -File |
    Where-Object Extension -in '.js', '.jsx' | ForEach-Object {
        $taskText = [System.IO.File]::ReadAllText($_.FullName)
        foreach ($taskMatch in [regex]::Matches($taskText, $taskPattern)) {
            $taskName = $taskMatch.Groups['function'].Value
            if (-not $taskName) { $taskName = $taskMatch.Groups['arrow'].Value }
            $taskPrefix = $taskText.Substring(0, $taskMatch.Index).TrimEnd()
            if (-not $taskPrefix.EndsWith('*/') -or $taskPrefix.LastIndexOf('/**') -lt 0) {
                $taskMissing += "$($_.Name):$taskName"
            }
            $taskCount++
        }
    }
"Named frontend functions: $taskCount; missing JSDoc: $($taskMissing.Count)"
if ($taskMissing.Count) { $taskMissing; exit 1 }
