$ErrorActionPreference = 'Stop'

$composeRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = (Resolve-Path (Join-Path $composeRoot '..\..')).Path
$copyRoot = Join-Path ([System.IO.Path]::GetTempPath()) `
    ('business-invoice-phase0-clean-' + [guid]::NewGuid().ToString('N'))

try {
    New-Item -ItemType Directory -Path $copyRoot | Out-Null
    $null = & robocopy $repositoryRoot $copyRoot /E `
        /XD .git .tools node_modules bin obj dist artifacts `
        /XF '*.user' '*.log'
    if ($LASTEXITCODE -ge 8) {
        throw "robocopy failed with exit code $LASTEXITCODE."
    }

    & (Join-Path $copyRoot 'spikes\compose\verify-compose.ps1')
    if ($LASTEXITCODE -ne 0) {
        throw "Clean-copy Compose verification failed with exit code $LASTEXITCODE."
    }
    Write-Output "PASS clean-copy=$copyRoot"
}
finally {
    if (Test-Path -LiteralPath $copyRoot) {
        $resolvedCopy = (Resolve-Path -LiteralPath $copyRoot).Path
        $resolvedTemp = (Resolve-Path -LiteralPath ([System.IO.Path]::GetTempPath())).Path
        if (-not $resolvedCopy.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove unexpected path: $resolvedCopy"
        }
        Remove-Item -LiteralPath $resolvedCopy -Recurse -Force
    }
}
