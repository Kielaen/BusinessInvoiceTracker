[CmdletBinding()]
param(
    [string]$DatabaseContainer = 'business-invoice-tracker-postgres-1'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sqlPath = Join-Path ([System.IO.Path]::GetTempPath()) `
    ('invoice-migrations-' + [guid]::NewGuid().ToString('N') + '.sql')

function Assert-NativeSuccess([string]$operation) {
    if ($LASTEXITCODE -ne 0) {
        throw "$operation failed with exit code $LASTEXITCODE."
    }
}

function Get-DatabaseInventory {
    $sql = @'
SELECT concat(
  (SELECT count(*) FROM "__EFMigrationsHistory"), '|',
  (SELECT count(*) FROM "AspNetUsers"), '|',
  (SELECT count(*) FROM businesses));
'@
    $result = $sql | docker exec -i $DatabaseContainer `
        psql -v ON_ERROR_STOP=1 -U invoice -d invoice -At
    Assert-NativeSuccess 'database inventory query'
    return ($result | Select-Object -Last 1).Trim()
}

try {
    Push-Location $repositoryRoot
    try {
        dotnet tool restore
        Assert-NativeSuccess 'repository-local tool restore'

        $env:ConnectionStrings__InvoiceDatabase =
            'Host=127.0.0.1;Database=design;Username=design;Password=design'
        dotnet ef migrations has-pending-model-changes `
            --project src/Invoice.Infrastructure/Invoice.Infrastructure.csproj `
            --startup-project src/Invoice.Api/Invoice.Api.csproj `
            --configuration Release --no-build
        Assert-NativeSuccess 'pending model change check'

        dotnet ef migrations script --idempotent `
            --project src/Invoice.Infrastructure/Invoice.Infrastructure.csproj `
            --startup-project src/Invoice.Api/Invoice.Api.csproj `
            --configuration Release --no-build --output $sqlPath
        Assert-NativeSuccess 'idempotent migration SQL generation'
        $sql = Get-Content -LiteralPath $sqlPath -Raw
        if ($sql.Length -lt 1000 -or
            $sql -notmatch '__EFMigrationsHistory' -or
            $sql -notmatch '20260803195135_AddDashboardQueryIndexes') {
            throw 'Generated SQL does not contain the expected reviewed migration history.'
        }
        if ($sql -match '(?im)^\s*(DROP\s+(TABLE|COLUMN)|TRUNCATE|DELETE\s+FROM)') {
            throw 'Generated forward migration contains an unreviewed destructive statement.'
        }
        Write-Output "Migration SQL bytes=$($sql.Length) sha256=$((Get-FileHash $sqlPath -Algorithm SHA256).Hash)"

        dotnet test tests/Invoice.Tests/Invoice.Tests.csproj `
            --configuration Release --no-build `
            --filter 'FullyQualifiedName~MigrationAppliesFromAllSupportedSnapshots'
        Assert-NativeSuccess 'empty and supported-snapshot migration test'

        $before = Get-DatabaseInventory
        docker compose --env-file .env.example run --rm --no-deps `
            --env Logging__LogLevel__Microsoft=Warning migrate
        Assert-NativeSuccess 'first one-shot migration rerun'
        $afterFirst = Get-DatabaseInventory
        docker compose --env-file .env.example run --rm --no-deps `
            --env Logging__LogLevel__Microsoft=Warning migrate
        Assert-NativeSuccess 'second one-shot migration rerun'
        $afterSecond = Get-DatabaseInventory
        if ($before -ne $afterFirst -or $afterFirst -ne $afterSecond) {
            throw "Repeat migration changed stable inventory: before=$before first=$afterFirst second=$afterSecond"
        }

        $ready = Invoke-WebRequest -UseBasicParsing `
            http://127.0.0.1:8080/health/ready
        if ($ready.StatusCode -ne 200) {
            throw "API readiness after migration rerun was $($ready.StatusCode)."
        }
        Write-Output "Migration inventory=$afterSecond readiness=$($ready.StatusCode)"
        Write-Output 'PASS Phase 11 migration deployment verification'
    }
    finally {
        Pop-Location
    }
}
finally {
    Remove-Item Env:ConnectionStrings__InvoiceDatabase -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $sqlPath) {
        Remove-Item -LiteralPath $sqlPath -Force
    }
}
