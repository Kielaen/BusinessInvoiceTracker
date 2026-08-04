[CmdletBinding()]
param(
    [string]$DatabaseContainer = 'business-invoice-tracker-postgres-1'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$token = [guid]::NewGuid().ToString('N').Substring(0, 12)
$backupDatabase = "phase11_restore_$token"
$privilegeDatabase = "phase11_privilege_$token"
$migratorRole = "phase11_migrator_$token"
$applicationRole = "phase11_app_$token"
$backupPath = "/tmp/phase11-backup-$token.dump"
$migrationSqlPath = Join-Path ([System.IO.Path]::GetTempPath()) `
    "phase11-migrations-$token.sql"
$previousSqlPath = Join-Path ([System.IO.Path]::GetTempPath()) `
    "phase11-previous-$token.sql"
$previousMigration = '20260801171347_AddNotificationRetryCycle'
$migratorPassword = "Migration-local-only-$token!"
$applicationPassword = "Application-local-only-$token!"

function Assert-NativeSuccess([string]$operation) {
    if ($LASTEXITCODE -ne 0) {
        throw "$operation failed with exit code $LASTEXITCODE."
    }
}

function Invoke-PostgresSql {
    param(
        [Parameter(Mandatory)]
        [string]$Database,
        [Parameter(Mandatory)]
        [string]$User,
        [Parameter(Mandatory)]
        [string]$Sql,
        [string]$Password
    )

    $arguments = @('exec')
    if (-not [string]::IsNullOrWhiteSpace($Password)) {
        $arguments += @('-e', "PGPASSWORD=$Password")
    }
    $arguments += @(
        '-i',
        $DatabaseContainer,
        'psql',
        '-v',
        'ON_ERROR_STOP=1',
        '-U',
        $User,
        '-d',
        $Database,
        '-At'
    )
    $result = $Sql | & docker @arguments
    Assert-NativeSuccess "PostgreSQL command as $User on $Database"
    return $result
}

function Get-Inventory([string]$database) {
    $sql = @'
SELECT concat(
  (SELECT count(*) FROM "__EFMigrationsHistory"), '|',
  (SELECT count(*) FROM "AspNetUsers"), '|',
  (SELECT count(*) FROM businesses), '|',
  (SELECT count(*) FROM customers), '|',
  (SELECT count(*) FROM invoices), '|',
  (SELECT count(*) FROM payments), '|',
  (SELECT count(*) FROM outbox_messages), '|',
  (SELECT count(*) FROM audit_entries));
'@
    return ((Invoke-PostgresSql -Database $database -User invoice -Sql $sql) |
        Select-Object -Last 1).Trim()
}

function Remove-TemporaryDatabase([string]$database) {
    $exists = Invoke-PostgresSql -Database invoice -User invoice -Sql `
        "SELECT 1 FROM pg_database WHERE datname = '$database';"
    if (($exists | Select-Object -Last 1) -eq '1') {
        Invoke-PostgresSql -Database invoice -User invoice -Sql `
            "DROP DATABASE `"$database`" WITH (FORCE);" | Out-Null
    }
}

try {
    Push-Location $repositoryRoot
    try {
        dotnet tool restore
        Assert-NativeSuccess 'repository-local tool restore'

        $previousDesignConnection = $env:ConnectionStrings__InvoiceDatabase
        $env:ConnectionStrings__InvoiceDatabase =
            'Host=127.0.0.1;Database=design;Username=design;Password=design'
        dotnet ef migrations script --idempotent `
            --project src/Invoice.Infrastructure/Invoice.Infrastructure.csproj `
            --startup-project src/Invoice.Api/Invoice.Api.csproj `
            --configuration Release --no-build --output $migrationSqlPath
        Assert-NativeSuccess 'full idempotent SQL generation'
        dotnet ef migrations script 0 $previousMigration `
            --project src/Invoice.Infrastructure/Invoice.Infrastructure.csproj `
            --startup-project src/Invoice.Api/Invoice.Api.csproj `
            --configuration Release --no-build --output $previousSqlPath
        Assert-NativeSuccess 'previous compatible schema SQL generation'

        $latestMigrationSource = Get-Content -LiteralPath `
            'src/Invoice.Infrastructure/Persistence/Migrations/20260803195135_AddDashboardQueryIndexes.cs' `
            -Raw
        if ($latestMigrationSource -match 'AddColumn|AlterColumn|DropColumn|DropTable' -or
            $latestMigrationSource -notmatch 'CreateIndex') {
            throw 'Latest migration is not the reviewed additive index-only change.'
        }

        $sourceInventory = Get-Inventory 'invoice'
        docker exec $DatabaseContainer pg_dump -U invoice -d invoice `
            --format=custom --no-owner --file=$backupPath
        Assert-NativeSuccess 'database backup'
        Invoke-PostgresSql -Database invoice -User invoice -Sql `
            "CREATE DATABASE `"$backupDatabase`";" | Out-Null
        docker exec $DatabaseContainer pg_restore -U invoice `
            --dbname=$backupDatabase --exit-on-error --no-owner $backupPath
        Assert-NativeSuccess 'database restore'
        $restoredInventory = Get-Inventory $backupDatabase
        if ($sourceInventory -ne $restoredInventory) {
            throw "Restored inventory mismatch: source=$sourceInventory restored=$restoredInventory"
        }

        $adminSql = @"
CREATE ROLE "$migratorRole" LOGIN PASSWORD '$migratorPassword';
CREATE ROLE "$applicationRole" LOGIN PASSWORD '$applicationPassword';
CREATE DATABASE "$privilegeDatabase" OWNER "$migratorRole";
"@
        Invoke-PostgresSql -Database invoice -User invoice -Sql $adminSql |
            Out-Null

        $previousSql = Get-Content -LiteralPath $previousSqlPath -Raw
        Invoke-PostgresSql -Database $privilegeDatabase `
            -User $migratorRole -Password $migratorPassword `
            -Sql $previousSql | Out-Null
        $grantSql = @"
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
GRANT CONNECT ON DATABASE "$privilegeDatabase" TO "$applicationRole";
GRANT USAGE ON SCHEMA public TO "$applicationRole";
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO "$applicationRole";
GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA public TO "$applicationRole";
ALTER DEFAULT PRIVILEGES FOR ROLE "$migratorRole" IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO "$applicationRole";
ALTER DEFAULT PRIVILEGES FOR ROLE "$migratorRole" IN SCHEMA public
  GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO "$applicationRole";
CREATE TABLE privilege_probe (
  id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  marker text NOT NULL
);
"@
        Invoke-PostgresSql -Database $privilegeDatabase `
            -User $migratorRole -Password $migratorPassword -Sql $grantSql |
            Out-Null

        $beforeCompatibility = Invoke-PostgresSql `
            -Database $privilegeDatabase `
            -User $applicationRole `
            -Password $applicationPassword `
            -Sql @'
INSERT INTO privilege_probe (marker) VALUES ('before-latest');
SELECT concat(
  (SELECT count(*) FROM businesses), '|',
  (SELECT count(*) FROM invoices), '|',
  (SELECT count(*) FROM privilege_probe));
'@
        $beforeCompatibility = ($beforeCompatibility | Select-Object -Last 1).Trim()

        $migrationSql = Get-Content -LiteralPath $migrationSqlPath -Raw
        Invoke-PostgresSql -Database $privilegeDatabase `
            -User $migratorRole -Password $migratorPassword `
            -Sql $migrationSql | Out-Null
        $afterCompatibility = Invoke-PostgresSql `
            -Database $privilegeDatabase `
            -User $applicationRole `
            -Password $applicationPassword `
            -Sql @'
INSERT INTO privilege_probe (marker) VALUES ('after-latest');
SELECT concat(
  (SELECT count(*) FROM businesses), '|',
  (SELECT count(*) FROM invoices), '|',
  (SELECT count(*) FROM privilege_probe), '|',
  (SELECT count(*) FROM "__EFMigrationsHistory"));
'@
        $afterCompatibility = ($afterCompatibility | Select-Object -Last 1).Trim()

        $previousErrorAction = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        $deniedOutput = @'
CREATE TABLE application_must_not_create_schema (id integer);
'@ | docker exec -e "PGPASSWORD=$applicationPassword" -i `
            $DatabaseContainer psql -v ON_ERROR_STOP=1 `
            -U $applicationRole -d $privilegeDatabase -At 2>&1
        $deniedExitCode = $LASTEXITCODE
        $ErrorActionPreference = $previousErrorAction
        if ($deniedExitCode -eq 0) {
            throw 'Steady-state application role unexpectedly created schema.'
        }
        if (($deniedOutput -join "`n") -notmatch 'permission denied') {
            throw 'Application schema denial did not produce the expected PostgreSQL evidence.'
        }

        $ready = (Invoke-WebRequest -UseBasicParsing `
            http://127.0.0.1:8080/health/ready).StatusCode
        if ($ready -ne 200) {
            throw "Main application readiness changed during isolated rehearsal: $ready"
        }

        Write-Output `
            "Backup inventory=$sourceInventory restored=$restoredInventory"
        Write-Output `
            "Compatibility before=$beforeCompatibility after=$afterCompatibility readiness=$ready"
        Write-Output `
            'Privileges migratorSchema=allowed applicationDml=allowed applicationSchema=denied'
        Write-Output `
            'PASS Phase 11 database backup, privilege and compatibility verification'
    }
    finally {
        if ($null -eq $previousDesignConnection) {
            Remove-Item Env:ConnectionStrings__InvoiceDatabase `
                -ErrorAction SilentlyContinue
        }
        else {
            $env:ConnectionStrings__InvoiceDatabase = $previousDesignConnection
        }
        Pop-Location
    }
}
finally {
    Remove-TemporaryDatabase $backupDatabase
    Remove-TemporaryDatabase $privilegeDatabase
    foreach ($role in @($applicationRole, $migratorRole)) {
        $roleExists = Invoke-PostgresSql -Database invoice -User invoice -Sql `
            "SELECT 1 FROM pg_roles WHERE rolname = '$role';"
        if (($roleExists | Select-Object -Last 1) -eq '1') {
            Invoke-PostgresSql -Database invoice -User invoice -Sql `
                "DROP ROLE `"$role`";" | Out-Null
        }
    }
    docker exec $DatabaseContainer rm -f $backupPath
    if ($LASTEXITCODE -ne 0) {
        Write-Warning 'Could not remove the uniquely named temporary backup file.'
    }
    foreach ($path in @($migrationSqlPath, $previousSqlPath)) {
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Force
        }
    }
}
