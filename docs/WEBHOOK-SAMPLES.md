# Sample Webhook Requests (20, 30, 50)

Generated sample WhatsApp-style webhook payloads for load testing or demos.

## Files

| File | Description |
|------|-------------|
| `sample-webhook-requests-20.json` | Array of 20 webhook payloads |
| `sample-webhook-requests-30.json` | Array of 30 webhook payloads |
| `sample-webhook-requests-50.json` | Array of 50 webhook payloads |

Each payload has the same structure as the [WhatsApp Cloud API webhook](https://developers.facebook.com/docs/whatsapp/cloud-api/webhooks/components): one message per payload with `Order: Item x N, ...` style body. Message IDs, phones, and order items are varied.

## Regenerate samples

From repo root:

```powershell
.\docs\scripts\generate-webhook-samples.ps1
```

This overwrites the three JSON files with new random orders (Pizza, Burger, Coke, Fries, etc.).

## Send to webhook

Send all payloads from a file (one POST per payload). From repo root:

```powershell
# Send 20 requests to local API (default)
.\docs\scripts\send-webhook-samples.ps1 -Count 20

# Send 30 requests
.\docs\scripts\send-webhook-samples.ps1 -Count 30

# Send 50 requests to a custom URL
.\docs\scripts\send-webhook-samples.ps1 -Count 50 -BaseUrl "http://localhost:8080"
```

With Docker:

```powershell
.\docs\scripts\send-webhook-samples.ps1 -Count 20 -BaseUrl "http://localhost:8080"
```

## Single payload (curl)

To send one payload from the 20-file (first element):

```powershell
$payload = (Get-Content docs\sample-webhook-requests-20.json | ConvertFrom-Json)[0]
$body = $payload | ConvertTo-Json -Depth 10
curl.exe -X POST http://localhost:8080/webhook -H "Content-Type: application/json" -d $body
```

Or use the original single sample:

```bash
curl -X POST http://localhost:8080/webhook -H "Content-Type: application/json" -d @docs/sample-webhook-payload.json
```
