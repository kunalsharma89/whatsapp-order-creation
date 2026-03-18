# cURL Examples for WhatsApp Order Webhook

Base URL (local): `http://localhost:5000` or `https://localhost:5001` (adjust port from launchSettings.json).

## 1. Webhook verification (GET)

WhatsApp sends this when you subscribe the webhook. Respond with the challenge to verify.

```bash
curl -X GET "http://localhost:5000/webhook?hub.mode=subscribe&hub.verify_token=my-verify-token&hub.challenge=challenge123"
```

Expected response: `challenge123` (status 200).

Wrong token:

```bash
curl -X GET "http://localhost:5000/webhook?hub.mode=subscribe&hub.verify_token=wrong&hub.challenge=challenge123"
```

Expected: 401 Unauthorized.

---

## 2. Post webhook message (POST) – valid order

```bash
curl -X POST "http://localhost:5000/webhook" \
  -H "Content-Type: application/json" \
  -d @sample-webhook-payload.json
```

Or inline:

```bash
curl -X POST "http://localhost:5000/webhook" \
  -H "Content-Type: application/json" \
  -d '{
    "object": "whatsapp_business_account",
    "entry": [{
      "id": "123",
      "changes": [{
        "value": {
          "messaging_product": "whatsapp",
          "metadata": { "display_phone_number": "15551234567", "phone_number_id": "987" },
          "contacts": [{ "profile": { "name": "Jane" }, "wa_id": "15559876543" }],
          "messages": [{
            "from": "15559876543",
            "id": "msg-001",
            "timestamp": "1700000000",
            "type": "text",
            "text": { "body": "Order: Pizza x2, Coke x1" }
          }]
        }
      }]
    }]
  }'
```

Expected: 200 OK. Message is published to RabbitMQ and processed by Order Processing Service.

---

## 3. Post webhook – invalid order (for DLQ test)

Send a message that does not match the order format (e.g. no "Order:" or wrong pattern). It should be rejected and end in the DLQ after processing.

```bash
curl -X POST "http://localhost:5000/webhook" \
  -H "Content-Type: application/json" \
  -d '{
    "object": "whatsapp_business_account",
    "entry": [{
      "id": "123",
      "changes": [{
        "value": {
          "messaging_product": "whatsapp",
          "metadata": {},
          "contacts": [{ "wa_id": "15559876543" }],
          "messages": [{
            "from": "15559876543",
            "id": "msg-invalid-001",
            "timestamp": "1700000000",
            "type": "text",
            "text": { "body": "Hello, I just want to say hi" }
          }]
        }
      }]
    }]
  }'
```

Expected: 200 OK. Message is published; worker parses it, validation fails, message goes to DLQ.

---

## 4. Health checks

**Full health** (PostgreSQL + RabbitMQ):

```bash
curl -s "http://localhost:5000/health" | jq
```

**Readiness** (dependencies ready for traffic):

```bash
curl -s "http://localhost:5000/health/ready"
```

**Liveness** (process alive):

```bash
curl -s "http://localhost:5000/health/live"
```

**With correlation ID** (echoed in response and logs):

```bash
curl -v -X POST "http://localhost:5000/webhook" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-Id: my-test-correlation-123" \
  -d @sample-webhook-payload.json
```
