[CmdletBinding()]
param(
    [switch]$Build,
    [switch]$Cleanup,
    [ValidatePattern('^[a-z0-9][a-z0-9_-]+$')]
    [string]$ProjectName = 'business-invoice-tracker'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$baseUri = 'http://127.0.0.1:8080'
$replicaUri = 'http://127.0.0.1:5081'
$acmeBusinessId = '11111111-1111-4111-8111-111111111111'
$demoPassword = 'Phase2-local-only!'
$composeArguments = @(
    'compose',
    '--project-name',
    $ProjectName,
    '--env-file',
    '.env.example',
    '--profile',
    'replica-test'
)

function Invoke-Compose {
    & docker @composeArguments @args
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose failed with exit code $LASTEXITCODE."
    }
}

function Wait-ForHealthyContainer(
    [string]$containerName,
    [int]$seconds = 180
) {
    $deadline = (Get-Date).AddSeconds($seconds)
    do {
        $health = docker inspect --format '{{.State.Health.Status}}' `
            $containerName 2>$null
        if ($health -eq 'healthy') {
            return
        }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)

    throw "$containerName did not become healthy within $seconds seconds."
}

function Get-HttpStatus {
    param(
        [string]$Uri,
        [Microsoft.PowerShell.Commands.WebRequestSession]$Session,
        [string]$Method = 'Get',
        [hashtable]$Headers = @{}
    )

    try {
        $parameters = @{
            Uri = $Uri
            Method = $Method
            WebSession = $Session
            Headers = $Headers
            UseBasicParsing = $true
        }
        $response = Invoke-WebRequest @parameters
        return [int]$response.StatusCode
    }
    catch {
        if ($null -eq $_.Exception.Response) {
            throw
        }
        return [int]$_.Exception.Response.StatusCode
    }
}

function New-AuthenticatedSession([string]$email) {
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $antiforgery = Invoke-RestMethod "$baseUri/api/v1/auth/antiforgery" `
        -WebSession $session
    $headers = @{}
    $headers[$antiforgery.headerName] = $antiforgery.token
    $body = @{
        email = $email
        password = $demoPassword
    } | ConvertTo-Json
    $user = Invoke-RestMethod "$baseUri/api/v1/auth/login" `
        -Method Post `
        -WebSession $session `
        -Headers $headers `
        -ContentType 'application/json' `
        -Body $body

    return @{
        Session = $session
        User = $user
    }
}

function Assert-Status(
    [string]$name,
    [int]$actual,
    [int]$expected
) {
    if ($actual -ne $expected) {
        throw "$name returned $actual; expected $expected."
    }
}

Push-Location $repositoryRoot
try {
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & docker compose `
            --env-file scripts/fixtures/phase2-missing-demo-secret.env `
            config --quiet 2>&1 | Out-Null
        $missingSecretExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    if ($missingSecretExitCode -eq 0) {
        throw 'Compose accepted a missing demonstration password.'
    }

    $upArguments = @('up', '--detach')
    if ($Build) {
        $upArguments += '--build'
    }
    Invoke-Compose @upArguments

    Wait-ForHealthyContainer "$ProjectName-postgres-1"
    Wait-ForHealthyContainer "$ProjectName-api-1"
    Wait-ForHealthyContainer "$ProjectName-api-replica-1"

    $results = [ordered]@{}
    try {
        $ErrorActionPreference = 'Continue'
        $productionSeedOutput = & docker @composeArguments run --rm `
            --env ASPNETCORE_ENVIRONMENT=Production migrate 2>&1
        $productionSeedExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    if ($productionSeedExitCode -eq 0) {
        throw 'Demonstration seeding unexpectedly succeeded in Production.'
    }
    if (($productionSeedOutput -join "`n") -notmatch
        'permitted only in Development') {
        throw 'Production demonstration seeding failed for an unexpected reason.'
    }
    $results.missingDemoSecretRejected = 1
    $results.productionSeedRejected = 1

    $anonymous = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $results.anonymous = Get-HttpStatus `
        "$baseUri/api/v1/businesses/$acmeBusinessId" $anonymous
    Assert-Status 'anonymous tenant request' $results.anonymous 401

    $viewer = New-AuthenticatedSession 'viewer@acme.test'
    $results.viewerRead = Get-HttpStatus `
        "$baseUri/api/v1/businesses/$acmeBusinessId" $viewer.Session
    Assert-Status 'viewer tenant read' $results.viewerRead 200
    $results.viewerOwnerResource = Get-HttpStatus `
        "$baseUri/api/v1/businesses/$acmeBusinessId/memberships" `
        $viewer.Session
    Assert-Status 'viewer owner-only resource' $results.viewerOwnerResource 403

    $accountant = New-AuthenticatedSession 'accountant@acme.test'
    $results.accountantRead = Get-HttpStatus `
        "$baseUri/api/v1/businesses/$acmeBusinessId" $accountant.Session
    Assert-Status 'accountant tenant read' $results.accountantRead 200

    $otherOwner = New-AuthenticatedSession 'owner@other.test'
    $results.crossTenant = Get-HttpStatus `
        "$baseUri/api/v1/businesses/$acmeBusinessId" $otherOwner.Session
    Assert-Status 'cross-tenant request' $results.crossTenant 404

    $noMembership = New-AuthenticatedSession 'nomembership@test.local'
    $results.noMembership = Get-HttpStatus `
        "$baseUri/api/v1/businesses/$acmeBusinessId" $noMembership.Session
    Assert-Status 'missing membership request' $results.noMembership 404

    $disabled = New-AuthenticatedSession 'disabled@acme.test'
    $results.inactiveMembership = Get-HttpStatus `
        "$baseUri/api/v1/businesses/$acmeBusinessId" $disabled.Session
    Assert-Status 'inactive membership request' $results.inactiveMembership 404

    $owner = New-AuthenticatedSession 'owner@acme.test'
    $results.ownerMemberships = Get-HttpStatus `
        "$baseUri/api/v1/businesses/$acmeBusinessId/memberships?page=1&pageSize=2" `
        $owner.Session
    Assert-Status 'owner membership list' $results.ownerMemberships 200
    $membershipPage = Invoke-RestMethod `
        "$baseUri/api/v1/businesses/$acmeBusinessId/memberships?page=1&pageSize=2" `
        -WebSession $owner.Session
    if ($membershipPage.page -ne 1 -or $membershipPage.pageSize -ne 2 -or
        $membershipPage.items.Count -gt 2 -or $membershipPage.totalCount -lt 3) {
        throw 'Owner membership collection did not return bounded pagination metadata.'
    }
    $businessPage = Invoke-RestMethod `
        "$baseUri/api/v1/businesses?page=1&pageSize=1" `
        -WebSession $owner.Session
    if ($businessPage.page -ne 1 -or $businessPage.pageSize -ne 1 -or
        $businessPage.items.Count -ne 1) {
        throw 'Business collection did not return bounded pagination metadata.'
    }
    $results.invalidBusinessPage = Get-HttpStatus `
        "$baseUri/api/v1/businesses?page=0&pageSize=101" $owner.Session
    Assert-Status 'invalid business pagination' $results.invalidBusinessPage 400
    $results.missingCsrf = Get-HttpStatus `
        "$baseUri/api/v1/auth/logout" $owner.Session 'Post'
    Assert-Status 'missing antiforgery token' $results.missingCsrf 400

    $results.replicaAcceptsCookie = Get-HttpStatus `
        "$replicaUri/api/v1/auth/me" $owner.Session
    Assert-Status 'replica authentication' $results.replicaAcceptsCookie 200

    $replicaToken = Invoke-RestMethod "$replicaUri/api/v1/auth/antiforgery" `
        -WebSession $owner.Session
    $replicaHeaders = @{}
    $replicaHeaders[$replicaToken.headerName] = $replicaToken.token
    $results.replicaLogout = Get-HttpStatus `
        "$replicaUri/api/v1/auth/logout" `
        $owner.Session `
        'Post' `
        $replicaHeaders
    Assert-Status 'replica antiforgery and logout' $results.replicaLogout 204
    $results.loggedOutOnPrimary = Get-HttpStatus `
        "$baseUri/api/v1/auth/me" $owner.Session
    Assert-Status 'primary after replica logout' $results.loggedOutOnPrimary 401

    [pscustomobject]@{
        phase = 2
        gate = 'passed'
        acmeBusinessId = $acmeBusinessId
        checks = $results
    } | ConvertTo-Json -Depth 5
}
finally {
    if ($Cleanup) {
        Invoke-Compose down --volumes --remove-orphans
    }
    Pop-Location
}
