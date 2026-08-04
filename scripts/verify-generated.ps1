[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

function Get-ContractHash {
    $files = @(
        Join-Path $repositoryRoot 'contracts\Invoice.Api.json'
        Get-ChildItem (Join-Path $repositoryRoot 'web\invoice-web\src\generated') -File -Recurse |
            Sort-Object FullName |
            Select-Object -ExpandProperty FullName
    )
    $material = $files | ForEach-Object { "$_`n$((Get-FileHash $_ -Algorithm SHA256).Hash)" }
    $bytes = [Text.Encoding]::UTF8.GetBytes(($material -join "`n"))
    $stream = [IO.MemoryStream]::new($bytes)
    try { (Get-FileHash -InputStream $stream -Algorithm SHA256).Hash }
    finally { $stream.Dispose() }
}

Push-Location $repositoryRoot
try {
    $before = Get-ContractHash
    dotnet build BusinessInvoiceTracker.slnx --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'OpenAPI build failed.' }
    Push-Location 'web\invoice-web'
    try {
        npm run generate
        if ($LASTEXITCODE -ne 0) { throw 'TypeScript generation failed.' }
    }
    finally { Pop-Location }
    $after = Get-ContractHash
    if ($before -ne $after) {
        throw 'Generated contract artifacts had drift. Regenerated files are now present; review and rerun.'
    }
    Write-Host "Generated artifacts are deterministic and in sync ($after)."
}
finally { Pop-Location }
