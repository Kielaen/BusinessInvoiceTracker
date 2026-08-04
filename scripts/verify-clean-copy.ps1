[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$copyRoot = Join-Path ([System.IO.Path]::GetTempPath()) `
    ('business-invoice-phase2-clean-' + [guid]::NewGuid().ToString('N'))
$cleanProjectName = 'business-invoice-phase2-clean'

function Assert-NativeSuccess([string]$operation) {
    if ($LASTEXITCODE -ne 0) {
        throw "$operation failed with exit code $LASTEXITCODE."
    }
}

function Reset-ApiPair([string]$projectName) {
    docker compose --project-name $projectName `
        --env-file .env.example --profile replica-test stop api-dapr api
    Assert-NativeSuccess 'clean-copy API pair stop'
    docker compose --project-name $projectName `
        --env-file .env.example --profile replica-test start api
    Assert-NativeSuccess 'clean-copy API start'
    docker compose --project-name $projectName `
        --env-file .env.example --profile replica-test start api-dapr
    Assert-NativeSuccess 'clean-copy API sidecar start'
}

try {
    New-Item -ItemType Directory -Path $copyRoot | Out-Null
    $null = & robocopy $repositoryRoot $copyRoot /E `
        /XD .git .tools node_modules bin obj dist artifacts `
        /XF '*.user' '*.log'
    if ($LASTEXITCODE -ge 8) {
        throw "robocopy failed with exit code $LASTEXITCODE."
    }

    Push-Location $repositoryRoot
    try {
        docker compose --env-file .env.example `
            --profile replica-test down --remove-orphans
        Assert-NativeSuccess 'main-stack stop'
    }
    finally {
        Pop-Location
    }

    & (Join-Path $copyRoot 'scripts\verify.ps1')
    if (-not $?) { throw 'clean-copy locked build and test verification failed.' }

    & (Join-Path $copyRoot 'scripts\verify-phase2.ps1') `
        -Build `
        -ProjectName $cleanProjectName
    if (-not $?) { throw 'clean-copy Phase 2 verification failed.' }
    & (Join-Path $copyRoot 'scripts\verify-phase4.ps1')
    if (-not $?) { throw 'clean-copy Phase 4 verification failed.' }
    # Earlier security gates intentionally exercise enough login attempts to
    # consume the in-memory fixed-window limiter. Restart the shared-network
    # API/sidecar pair between independently scoped phase gates; this preserves
    # the production limit while keeping the sequence deterministic.
    Reset-ApiPair $cleanProjectName
    $apiContainer = "$cleanProjectName-api-1"
    $deadline = (Get-Date).AddSeconds(180)
    do {
        $health = docker inspect --format '{{.State.Health.Status}}' `
            $apiContainer 2>$null
        if ($health -eq 'healthy') { break }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)
    if ($health -ne 'healthy') {
        throw "$apiContainer did not become healthy after restart."
    }
    Start-Sleep -Seconds 5
    & (Join-Path $copyRoot 'scripts\verify-phase5.ps1')
    if (-not $?) { throw 'clean-copy Phase 5 verification failed.' }
    & (Join-Path $copyRoot 'scripts\verify-phase6.ps1')
    if (-not $?) { throw 'clean-copy Phase 6 verification failed.' }
    Reset-ApiPair $cleanProjectName
    $deadline = (Get-Date).AddSeconds(180)
    do {
        $health = docker inspect --format '{{.State.Health.Status}}' `
            $apiContainer 2>$null
        if ($health -eq 'healthy') { break }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)
    if ($health -ne 'healthy') {
        throw "$apiContainer did not become healthy before Phase 7."
    }
    Start-Sleep -Seconds 5
    & (Join-Path $copyRoot 'scripts\verify-phase7.ps1') `
        -DatabaseContainer "$cleanProjectName-postgres-1"
    if (-not $?) { throw 'clean-copy Phase 7 verification failed.' }
    $previousComposeProjectName = $env:COMPOSE_PROJECT_NAME
    $env:COMPOSE_PROJECT_NAME = $cleanProjectName
    Push-Location $copyRoot
    try {
        & (Join-Path $copyRoot 'scripts\verify-phase8-happy-path.ps1') `
            -DatabaseContainer "$cleanProjectName-postgres-1"
        if (-not $?) { throw 'clean-copy Phase 8 happy-path verification failed.' }
        & (Join-Path $copyRoot 'scripts\verify-phase8-reschedule.ps1') `
            -DatabaseContainer "$cleanProjectName-postgres-1"
        if (-not $?) { throw 'clean-copy Phase 8 timer/reschedule verification failed.' }
        & (Join-Path $copyRoot 'scripts\verify-phase8-notification-recovery.ps1') `
            -DatabaseContainer "$cleanProjectName-postgres-1"
        if (-not $?) { throw 'clean-copy Phase 8 delivery recovery verification failed.' }
        & (Join-Path $copyRoot 'scripts\verify-phase10-notification-diagnostics.ps1') `
            -ProjectName $cleanProjectName
        if (-not $?) { throw 'clean-copy Phase 10 notification diagnostics failed.' }
        & (Join-Path $copyRoot 'scripts\verify-phase8-terminal-races.ps1') `
            -DatabaseContainer "$cleanProjectName-postgres-1"
        if (-not $?) { throw 'clean-copy Phase 8 terminal-race verification failed.' }
        & (Join-Path $copyRoot 'scripts\verify-phase8-reconciliation.ps1') `
            -DatabaseContainer "$cleanProjectName-postgres-1"
        if (-not $?) { throw 'clean-copy Phase 8 reconciliation verification failed.' }
        docker compose --project-name $cleanProjectName --env-file .env.example `
            --profile replica-test up -d api-replica api-replica-dapr --wait
        Assert-NativeSuccess 'clean-copy Phase 8 replica start'
        & (Join-Path $copyRoot 'scripts\verify-phase8-two-publishers.ps1') `
            -DatabaseContainer "$cleanProjectName-postgres-1" `
            -PrimaryApiContainer "$cleanProjectName-api-1" `
            -ReplicaApiContainer "$cleanProjectName-api-replica-1"
        if (-not $?) { throw 'clean-copy Phase 8 publisher verification failed.' }
        docker compose --project-name $cleanProjectName --env-file .env.example `
            --profile replica-test stop api-replica api-replica-dapr
        Assert-NativeSuccess 'clean-copy Phase 8 replica stop'
        Reset-ApiPair $cleanProjectName
        $deadline = (Get-Date).AddSeconds(180)
        do {
            $health = docker inspect --format '{{.State.Health.Status}}' `
                $apiContainer 2>$null
            if ($health -eq 'healthy') { break }
            Start-Sleep -Seconds 2
        } while ((Get-Date) -lt $deadline)
        if ($health -ne 'healthy') {
            throw "$apiContainer did not become healthy before Phase 9."
        }
        Start-Sleep -Seconds 5
        & (Join-Path $copyRoot 'scripts\verify-phase9.ps1') `
            -DatabaseContainer "$cleanProjectName-postgres-1"
        if (-not $?) { throw 'clean-copy Phase 9 verification failed.' }
        & (Join-Path $copyRoot 'scripts\verify-phase10.ps1') `
            -ProjectName $cleanProjectName
        if (-not $?) { throw 'clean-copy Phase 10 resilience verification failed.' }
        & (Join-Path $copyRoot 'scripts\verify-phase11.ps1') `
            -ProjectName $cleanProjectName
        if (-not $?) { throw 'clean-copy Phase 11 hardening verification failed.' }
    }
    finally {
        Pop-Location
        $env:COMPOSE_PROJECT_NAME = $previousComposeProjectName
    }
    Write-Output "PASS clean-copy=$copyRoot"
}
finally {
    if (Test-Path -LiteralPath (Join-Path $copyRoot 'compose.yaml')) {
        Push-Location $copyRoot
        try {
            docker compose --project-name $cleanProjectName `
                --env-file .env.example --profile replica-test `
                down --volumes --remove-orphans
            Assert-NativeSuccess 'clean-copy stack cleanup'
        }
        finally {
            Pop-Location
        }
    }

    Push-Location $repositoryRoot
    try {
        docker compose --env-file .env.example `
            --profile replica-test up --detach
        Assert-NativeSuccess 'main-stack restore'
    }
    finally {
        Pop-Location
    }

    if (Test-Path -LiteralPath $copyRoot) {
        $resolvedCopy = (Resolve-Path -LiteralPath $copyRoot).Path
        $resolvedTemp = (
            Resolve-Path -LiteralPath ([System.IO.Path]::GetTempPath())
        ).Path
        if (-not $resolvedCopy.StartsWith(
            $resolvedTemp,
            [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove unexpected path: $resolvedCopy"
        }
        Remove-Item -LiteralPath $resolvedCopy -Recurse -Force
    }
}
