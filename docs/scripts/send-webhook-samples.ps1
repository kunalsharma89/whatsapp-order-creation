# Sends sample webhook payloads from generated JSON files to the webhook endpoint.
# Usage: .\send-webhook-samples.ps1 [-Count 20|30|50] [-BaseUrl "http://localhost:8080"]
# Run from repo root: .\docs\scripts\send-webhook-samples.ps1 -Count 20

param(
    [ValidateSet(20, 30, 50)]
    [int] $Count = 20,
    [string] $BaseUrl = "http://localhost:8080"
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$jsonPath = Join-Path (Join-Path $scriptDir "..") "sample-webhook-requests-$Count.json"
if (-not (Test-Path $jsonPath)) {
    Write-Error "File not found: $jsonPath. Run generate-webhook-samples.ps1 first."
    exit 1
}

# Parse array and send each payload as JSON (preserves lowercase keys)
$raw = Get-Content -Path $jsonPath -Raw
$payloads = $raw | ConvertFrom-Json
$uri = "$BaseUrl/webhook"
$sent = 0
$failed = 0

foreach ($p in $payloads) {
    # Use -Compress to avoid extra whitespace; PowerShell 7+ keeps JSON key case from source
    $body = $p | ConvertTo-Json -Depth 15 -Compress
    try {
        Invoke-RestMethod -Uri $uri -Method Post -Body $body -ContentType "application/json; charset=utf-8" -ErrorAction Stop | Out-Null
        $sent++
        Write-Host "POST $sent/$Count -> 200"
    }
    catch {
        $failed++
        Write-Warning "POST failed: $_"
    }
}

Write-Host "Done. Sent: $sent, Failed: $failed"
