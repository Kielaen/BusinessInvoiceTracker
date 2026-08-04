[CmdletBinding()]
param(
    [ValidatePattern('^[a-z0-9][a-z0-9_-]+$')]
    [string]$ProjectName = 'business-invoice-tracker',
    [switch]$SkipImageBuild
)

$ErrorActionPreference = 'Stop'
$databaseContainer = "$ProjectName-postgres-1"

& "$PSScriptRoot\verify-phase11-security.ps1" -ProjectName $ProjectName
if (-not $?) { throw 'Phase 11 security verification failed.' }

& "$PSScriptRoot\verify-phase11-migrations.ps1" `
    -DatabaseContainer $databaseContainer
if (-not $?) { throw 'Phase 11 migration verification failed.' }

& "$PSScriptRoot\verify-phase11-database-operations.ps1" `
    -DatabaseContainer $databaseContainer
if (-not $?) { throw 'Phase 11 database operations verification failed.' }

& "$PSScriptRoot\verify-phase11-performance.ps1"
if (-not $?) { throw 'Phase 11 performance verification failed.' }

& "$PSScriptRoot\verify-phase11-shutdown.ps1" -ProjectName $ProjectName
if (-not $?) { throw 'Phase 11 shutdown verification failed.' }

& "$PSScriptRoot\verify-phase11-supply-chain.ps1" `
    -SkipBuild:$SkipImageBuild
if (-not $?) { throw 'Phase 11 supply-chain verification failed.' }

Write-Output 'PASS exact Phase 11 focused verification suite'
