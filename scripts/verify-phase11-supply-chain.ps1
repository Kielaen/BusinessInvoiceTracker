[CmdletBinding()]
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$scanRoot = Join-Path ([System.IO.Path]::GetTempPath()) `
    ('invoice-phase11-source-' + [guid]::NewGuid().ToString('N'))
$reportRoot = Join-Path ([System.IO.Path]::GetTempPath()) `
    ('invoice-phase11-reports-' + [guid]::NewGuid().ToString('N'))
$gitleaksImage = 'ghcr.io/gitleaks/gitleaks:v8.30.1@sha256:c00b6bd0aeb3071cbcb79009cb16a60dd9e0a7c60e2be9ab65d25e6bc8abbb7f'
$images = [ordered]@{
    api = 'business-invoice-phase11-api:latest'
    automation = 'business-invoice-phase11-automation:latest'
    web = 'business-invoice-phase11-web:latest'
}

function Assert-NativeSuccess([string]$operation) {
    if ($LASTEXITCODE -ne 0) {
        throw "$operation failed with exit code $LASTEXITCODE."
    }
}

try {
    New-Item -ItemType Directory -Path $scanRoot, $reportRoot | Out-Null

    # Scan exactly the clean repository material: runtime keys, build output,
    # dependencies and local environment files are deliberately not copyable.
    $null = & robocopy $repositoryRoot $scanRoot /E `
        /XD .git .tools artifacts node_modules bin obj dist coverage TestResults `
        /XF '.env' '.env.*' '*.log' '*.user' '*.tsbuildinfo'
    if ($LASTEXITCODE -ge 8) {
        throw "Clean source copy failed with robocopy exit code $LASTEXITCODE."
    }
    Copy-Item -LiteralPath (Join-Path $repositoryRoot '.env.example') `
        -Destination (Join-Path $scanRoot '.env.example') -Force

    $sourceMount = $scanRoot.Replace('\', '/')
    docker run --rm -v "${sourceMount}:/repo:ro" $gitleaksImage `
        dir /repo --no-banner --redact --exit-code 1
    Assert-NativeSuccess 'clean-repository secret scan'

    $workflowFiles = Get-ChildItem (Join-Path $scanRoot '.github\workflows') `
        -Filter '*.yml' -File
    foreach ($workflow in $workflowFiles) {
        $actionReferences = Select-String -LiteralPath $workflow.FullName `
            -Pattern '^\s*uses:\s*[^#\s]+@([^\s#]+)'
        foreach ($reference in $actionReferences) {
            $version = $reference.Matches[0].Groups[1].Value
            if ($version -notmatch '^[0-9a-f]{40}$') {
                throw "Unpinned action in $($workflow.Name): $($reference.Line.Trim())"
            }
        }
    }

    $containerReferences = Select-String `
        -Path (Join-Path $scanRoot 'Dockerfile'), (Join-Path $scanRoot 'compose*.yaml') `
        -Pattern '^\s*(FROM|image:)\s+([^\s]+)' -AllMatches
    foreach ($reference in $containerReferences) {
        $value = $reference.Matches[0].Groups[2].Value
        if ($value -match '^\$' -or $value -match '^dotnet-build$' -or
            $value -match '^web-build$') {
            continue
        }
        if ($value -notmatch '@sha256:[0-9a-f]{64}$') {
            throw "Mutable container reference: $value"
        }
    }

    if (-not $SkipBuild) {
        foreach ($target in $images.Keys) {
            docker build --target $target -t $images[$target] $repositoryRoot
            Assert-NativeSuccess "build $target final image"
        }
    }

    $expectedUsers = @{ api = '1654'; automation = '1654'; web = '101' }
    foreach ($target in $images.Keys) {
        $actualUser = docker image inspect $images[$target] `
            --format '{{.Config.User}}'
        Assert-NativeSuccess "inspect $target image identity"
        if ($actualUser.Trim() -ne $expectedUsers[$target]) {
            throw "$target image user '$actualUser' is not expected non-root user '$($expectedUsers[$target])'."
        }

        docker scout cves --only-severity critical,high `
            --only-vuln-packages --format only-packages --exit-code `
            "local://$($images[$target])"
        Assert-NativeSuccess "$target critical/high image scan"

        $sbomPath = Join-Path $reportRoot "$target.spdx.json"
        docker scout sbom --format spdx --output $sbomPath `
            "local://$($images[$target])"
        Assert-NativeSuccess "$target SBOM generation"
        $sbom = Get-Content -LiteralPath $sbomPath -Raw | ConvertFrom-Json
        if (-not $sbom.packages -or $sbom.packages.Count -lt 1) {
            throw "$target SBOM did not contain packages."
        }
        $hash = (Get-FileHash -LiteralPath $sbomPath -Algorithm SHA256).Hash
        Write-Output "SBOM target=$target packages=$($sbom.packages.Count) sha256=$hash"
    }

    Write-Output 'PASS Phase 11 supply-chain verification'
}
finally {
    foreach ($path in @($scanRoot, $reportRoot)) {
        if (Test-Path -LiteralPath $path) {
            $resolved = (Resolve-Path -LiteralPath $path).Path
            $temp = (Resolve-Path -LiteralPath ([System.IO.Path]::GetTempPath())).Path
            if (-not $resolved.StartsWith($temp, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Refusing to remove non-temporary path $resolved."
            }
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
    }
}
