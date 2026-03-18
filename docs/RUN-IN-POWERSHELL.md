# Run the solution from PowerShell

## Option A: Run everything in Docker (recommended)

From the **repo root** (`E:\Learning\whatsapp-order-creation`):

```powershell
# 1. Start all services (RabbitMQ, PostgreSQL, Webhook API, Order Processor, Notification)
docker compose up -d

# 2. Wait a few seconds for DB migrations and healthy containers, then check
docker compose ps

# 3. Test webhook and health
Invoke-RestMethod -Uri "http://localhost:8080/health" -Method Get
Invoke-RestMethod -Uri "http://localhost:8080/webhook?hub.mode=subscribe&hub.verify_token=my-verify-token&hub.challenge=hello" -Method Get

# 4. Send 20 sample webhook requests
.\docs\scripts\send-webhook-samples.ps1 -Count 20 -BaseUrl "http://localhost:8080"
```

**Useful Docker commands:**

```powershell
# View logs (all services)
docker compose logs -f

# Logs for one service
docker compose logs -f webhook-service

# Stop everything
docker compose down
```

---

## Option B: Run locally (without Docker for the .NET apps)

You need **RabbitMQ** and **PostgreSQL** running (e.g. from Docker). From repo root:

```powershell
# 1. Start only RabbitMQ and PostgreSQL
docker compose up -d rabbitmq postgres

# 2. Terminal 1 – Webhook API
cd src\WebhookService
dotnet run

# 3. Open a new PowerShell window (Terminal 2) – Order Processing Worker
cd E:\Learning\whatsapp-order-creation\src\OrderProcessingService
dotnet run

# 4. Open another PowerShell (Terminal 3) – Notification Worker
cd E:\Learning\whatsapp-order-creation\src\NotificationService
dotnet run
```

Then in a **fourth** PowerShell:

```powershell
cd E:\Learning\whatsapp-order-creation
.\docs\scripts\send-webhook-samples.ps1 -Count 20 -BaseUrl "http://localhost:5101"
```

(Use the port from WebhookService’s launchSettings.json, e.g. `5101` for http.)

---

## Option C: Run only infrastructure in Docker, .NET from IDE

```powershell
# Start RabbitMQ + PostgreSQL
docker compose up -d rabbitmq postgres
```

Then run **WebhookService**, **OrderProcessingService**, and **NotificationService** from Visual Studio or Cursor (F5 / Run). Use `http://localhost:8080` or the port your API uses when sending samples.

---

## Quick reference

| What              | Command |
|-------------------|--------|
| Start all in Docker | `docker compose up -d` |
| Send 20 samples    | `.\docs\scripts\send-webhook-samples.ps1 -Count 20 -BaseUrl "http://localhost:8080"` |
| Send 30 samples    | `.\docs\scripts\send-webhook-samples.ps1 -Count 30 -BaseUrl "http://localhost:8080"` |
| Send 50 samples    | `.\docs\scripts\send-webhook-samples.ps1 -Count 50 -BaseUrl "http://localhost:8080"` |
| Health check      | `Invoke-RestMethod http://localhost:8080/health` |
| Webhook verify     | `Invoke-RestMethod "http://localhost:8080/webhook?hub.mode=subscribe&hub.verify_token=my-verify-token&hub.challenge=test"` |
| Regenerate samples | `.\docs\scripts\generate-webhook-samples.ps1` |
| Stop Docker        | `docker compose down` |

**Note:** All commands assume you are in the repo root: `E:\Learning\whatsapp-order-creation`.
