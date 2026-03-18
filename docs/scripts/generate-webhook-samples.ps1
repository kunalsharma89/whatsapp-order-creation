# Generates sample webhook request JSON files with 20, 30, and 50 payloads.
# Run from repo root: .\docs\scripts\generate-webhook-samples.ps1

$items = @(
    "Pizza", "Burger", "Coke", "Fries", "Salad", "Pasta", "Sandwich",
    "Coffee", "Tea", "Juice", "Water", "Ice Cream", "Cake", "Soup", "Wrap"
)

function New-Payload($index, $orderBody) {
    $msgId = "wamid.sample" + $index.ToString("000") + "." + [guid]::NewGuid().ToString("N").Substring(0, 8)
    $phone = "1555" + (1000000 + $index).ToString()
    $ts = [string]([int][double]::Parse((Get-Date -UFormat %s)))
    @{
        object = "whatsapp_business_account"
        entry  = @(
            @{
                id      = (123456789 + $index).ToString()
                changes = @(
                    @{
                        value = @{
                            messaging_product = "whatsapp"
                            metadata         = @{
                                display_phone_number = "15551234567"
                                phone_number_id      = "987654321"
                            }
                            contacts = @(
                                @{
                                    profile = @{ name = "Customer $index" }
                                    wa_id   = $phone
                                }
                            )
                            messages = @(
                                @{
                                    from      = $phone
                                    id        = $msgId
                                    timestamp = $ts
                                    type      = "text"
                                    text      = @{ body = $orderBody }
                                }
                            )
                        }
                    }
                )
            }
        )
    }
}

function Get-RandomOrderBody {
    $count = Get-Random -Minimum 1 -Maximum 4
    $lines = @()
    for ($i = 0; $i -lt $count; $i++) {
        $item = $items | Get-Random
        $qty = Get-Random -Minimum 1 -Maximum 5
        $lines += "$item x$qty"
    }
    "Order: " + ($lines -join ", ")
}

$outDir = Join-Path $PSScriptRoot ".."
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

foreach ($n in 20, 30, 50) {
    $payloads = @()
    for ($i = 1; $i -le $n; $i++) {
        $payloads += New-Payload -index $i -orderBody (Get-RandomOrderBody)
    }
    $path = Join-Path $outDir "sample-webhook-requests-$n.json"
    $payloads | ConvertTo-Json -Depth 10 -Compress:$false | Set-Content -Path $path -Encoding UTF8
    Write-Host "Created $path with $n payloads"
}

Write-Host "Done. Use send-webhook-samples.ps1 to POST them to your webhook."
