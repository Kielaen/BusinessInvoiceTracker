[CmdletBinding()]
param(
    [int]$RequestCount = 120,
    [int]$Concurrency = 8,
    [int]$P95LimitMilliseconds = 2000
)

$ErrorActionPreference = 'Stop'
if ($RequestCount -lt 30 -or $RequestCount -gt 1000) {
    throw 'RequestCount must remain between 30 and 1000.'
}
if ($Concurrency -lt 1 -or $Concurrency -gt 32) {
    throw 'Concurrency must remain between 1 and 32.'
}

if (-not ('Phase11LoadProbe' -as [type])) {
    Add-Type -ReferencedAssemblies 'System.Net.Http.dll' -TypeDefinition @'
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

public sealed class Phase11ProbeResult
{
    public int StatusCode { get; set; }
    public long Milliseconds { get; set; }
    public string ErrorType { get; set; }
}

public static class Phase11LoadProbe
{
    public static async Task<Phase11ProbeResult[]> RunAsync(
        HttpClient client,
        string[] paths,
        int concurrency,
        CancellationToken cancellationToken)
    {
        using (var gate = new SemaphoreSlim(concurrency, concurrency))
        {
            var tasks = paths.Select(async path =>
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    using (var response = await client.GetAsync(
                        path,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken).ConfigureAwait(false))
                    {
                        return new Phase11ProbeResult
                        {
                            StatusCode = (int)response.StatusCode,
                            Milliseconds = stopwatch.ElapsedMilliseconds
                        };
                    }
                }
                catch (Exception exception)
                {
                    return new Phase11ProbeResult
                    {
                        StatusCode = 0,
                        Milliseconds = stopwatch.ElapsedMilliseconds,
                        ErrorType = exception.GetType().Name
                    };
                }
                finally
                {
                    gate.Release();
                }
            }).ToArray();
            return await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }
}
'@
}

$handler = [Net.Http.HttpClientHandler]::new()
$handler.CookieContainer = [Net.CookieContainer]::new()
$handler.MaxConnectionsPerServer = $Concurrency
$client = [Net.Http.HttpClient]::new($handler)
$client.BaseAddress = [Uri]'http://127.0.0.1:8080'
$client.Timeout = [TimeSpan]::FromSeconds(10)

try {
    $tokenResponse = $client.GetAsync('/api/v1/auth/antiforgery').GetAwaiter().GetResult()
    $tokenResponse.EnsureSuccessStatusCode() | Out-Null
    $token = ($tokenResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult() |
        ConvertFrom-Json)
    $loginRequest = [Net.Http.HttpRequestMessage]::new(
        [Net.Http.HttpMethod]::Post,
        '/api/v1/auth/login')
    $loginRequest.Headers.Add($token.headerName, [string]$token.token)
    $loginRequest.Content = [Net.Http.StringContent]::new(
        (@{
            email = 'viewer@acme.test'
            password = 'Phase2-local-only!'
        } | ConvertTo-Json),
        [Text.Encoding]::UTF8,
        'application/json')
    $loginResponse = $client.SendAsync($loginRequest).GetAwaiter().GetResult()
    $loginResponse.EnsureSuccessStatusCode() | Out-Null

    $business = '11111111-1111-4111-8111-111111111111'
    $routes = @(
        "/api/v1/businesses/$business/dashboard",
        "/api/v1/businesses/$business/invoices?page=1&pageSize=50&sort=updated&direction=desc",
        "/api/v1/businesses/$business/customers?page=1&pageSize=50"
    )
    $paths = for ($index = 0; $index -lt $RequestCount; $index++) {
        $routes[$index % $routes.Count]
    }
    $cancellation = [Threading.CancellationTokenSource]::new(
        [TimeSpan]::FromSeconds(60))
    $wall = [Diagnostics.Stopwatch]::StartNew()
    $results = [Phase11LoadProbe]::RunAsync(
        $client,
        [string[]]$paths,
        $Concurrency,
        $cancellation.Token).GetAwaiter().GetResult()
    $wall.Stop()

    $failures = @($results | Where-Object { $_.StatusCode -ne 200 })
    if ($failures.Count -gt 0) {
        $summary = $failures | Group-Object StatusCode, ErrorType |
            ForEach-Object { "$($_.Name)=$($_.Count)" }
        throw "Load profile produced failures: $($summary -join ', ')."
    }

    $ordered = @($results.Milliseconds | Sort-Object)
    $p50Index = [Math]::Min($ordered.Count - 1,
        [Math]::Ceiling($ordered.Count * 0.50) - 1)
    $p95Index = [Math]::Min($ordered.Count - 1,
        [Math]::Ceiling($ordered.Count * 0.95) - 1)
    $p50 = $ordered[$p50Index]
    $p95 = $ordered[$p95Index]
    $maximum = $ordered[-1]
    $rate = [Math]::Round($RequestCount / $wall.Elapsed.TotalSeconds, 2)
    if ($p95 -gt $P95LimitMilliseconds -or $maximum -gt 5000) {
        throw "Representative profile exceeded diagnostic bounds: p95=${p95}ms max=${maximum}ms."
    }
    Write-Output "Performance requests=$RequestCount concurrency=$Concurrency errors=0 p50=${p50}ms p95=${p95}ms max=${maximum}ms wall=$([Math]::Round($wall.Elapsed.TotalSeconds,2))s rate=${rate}/s"
    Write-Output 'PASS Phase 11 representative performance verification (diagnostic bound, not an SLO)'
}
finally {
    $client.Dispose()
    $handler.Dispose()
}
