[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$localDotnet = Join-Path $repositoryRoot '.tools\dotnet'

function Assert-NativeSuccess([string]$operation) {
    if ($LASTEXITCODE -ne 0) {
        throw "$operation failed with exit code $LASTEXITCODE."
    }
}

if (Test-Path (Join-Path $localDotnet 'dotnet.exe')) {
    $env:DOTNET_ROOT = $localDotnet
    $env:PATH = "$localDotnet;$env:PATH"
}

Push-Location $repositoryRoot
try {
    dotnet --version
    Assert-NativeSuccess 'dotnet --version'
    dotnet restore BusinessInvoiceTracker.slnx --locked-mode
    Assert-NativeSuccess 'locked .NET restore'
    dotnet build BusinessInvoiceTracker.slnx --configuration Release --no-restore
    Assert-NativeSuccess 'Release .NET build'
    dotnet format BusinessInvoiceTracker.slnx --verify-no-changes `
        --no-restore --verbosity minimal
    Assert-NativeSuccess '.NET formatting verification'
    dotnet tool restore
    Assert-NativeSuccess 'repository-local .NET tool restore'
    $previousDesignConnection = $env:ConnectionStrings__InvoiceDatabase
    try {
        $env:ConnectionStrings__InvoiceDatabase =
            'Host=127.0.0.1;Database=design;Username=design;Password=design'
        dotnet ef migrations has-pending-model-changes `
            --project src/Invoice.Infrastructure/Invoice.Infrastructure.csproj `
            --startup-project src/Invoice.Api/Invoice.Api.csproj `
            --configuration Release --no-build
        Assert-NativeSuccess 'pending EF model change verification'
    }
    finally {
        $env:ConnectionStrings__InvoiceDatabase = $previousDesignConnection
    }
    dotnet test BusinessInvoiceTracker.slnx --configuration Release --no-build
    Assert-NativeSuccess '.NET test suite'
    dotnet list BusinessInvoiceTracker.slnx package --vulnerable --include-transitive
    Assert-NativeSuccess 'NuGet vulnerability scan'

    Push-Location 'web\invoice-web'
    try {
        npm ci
        Assert-NativeSuccess 'npm ci'
        npm run generate
        Assert-NativeSuccess 'OpenAPI client generation'
        npm run test:coverage
        Assert-NativeSuccess 'frontend tests and coverage floor'
        npm run build
        Assert-NativeSuccess 'frontend build'
        npm run check:bundle
        Assert-NativeSuccess 'frontend bundle budget'
        npm audit --audit-level=high
        Assert-NativeSuccess 'npm audit'
    }
    finally {
        Pop-Location
    }

    docker compose --env-file .env.example config --quiet
    Assert-NativeSuccess 'Docker Compose configuration validation'
}
finally {
    Pop-Location
}
