# WhatsApp Order Automation System

Production-grade .NET 10 event-driven system: WhatsApp webhook → RabbitMQ (main/retry/DLQ) → Order Processing Worker → PostgreSQL, with optional Notification Service for confirmations.

## Architecture

- **WebhookService** (ASP.NET Core API): Receives WhatsApp webhook (GET verification, POST messages), publishes `OrderReceivedEvent` to RabbitMQ.
- **RabbitMQ**: Main queue `order-processing`, retry queue `order-retry` (exponential backoff per message), DLQ `order-dlq`.
- **OrderProcessingService** (Worker): Consumes from main queue, parses "Order: Item x N", validates, saves to PostgreSQL (idempotent by `MessageId`), publishes `OrderProcessedEvent` on success; retry with exponential backoff and circuit breaker (Polly); on failure after retries → DLQ.
- **NotificationService** (Worker): Consumes `OrderProcessedEvent`, mocks WhatsApp confirmation (logs message).
- **PostgreSQL**: Tables `Orders`, `OrderItems`, `FailedMessages`, `ProcessingLogs`.

## Prerequisites

- .NET 10 SDK
- Docker and Docker Compose (for RabbitMQ and PostgreSQL)

## Quick start

### 1. Start infrastructure

```bash
docker-compose up -d
```

- RabbitMQ: `localhost:5672` (AMQP), `http://localhost:15672` (management UI, guest/guest).
- PostgreSQL: `localhost:5432`, database `WhatsAppOrders`, user `postgres`, password `postgres`.

### 2. Database migrations

Migrations are **applied automatically** when you start the WebhookService. Just run the API (step 3); on first run it will create or update the database.

To run migrations manually instead (requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and the EF tool):

```bash
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/Infrastructure --startup-project src/WebhookService
```

### 3. Run the applications

Use three terminals.

**Terminal 1 – Webhook API**

```bash
cd src/WebhookService
dotnet run
```

**Terminal 2 – Order Processing Worker**

```bash
cd src/OrderProcessingService
dotnet run
```

**Terminal 3 – Notification Service (optional)**

```bash
cd src/NotificationService
dotnet run
```

### 4. Health checks

- **Full**: `GET /health` — PostgreSQL + RabbitMQ (JSON with status and entries).
- **Readiness**: `GET /health/ready` — same checks, for load balancers.
- **Liveness**: `GET /health/live` — process alive (no dependencies).

### 5. Verify webhook

```bash
curl "http://localhost:5000/webhook?hub.mode=subscribe&hub.verify_token=my-verify-token&hub.challenge=test"
```

Expected response body: `test`.

### 6. Send a test order

From the repo root:

```bash
curl -X POST "http://localhost:5000/webhook" -H "Content-Type: application/json" -d "@docs/sample-webhook-payload.json"
```

Check:

- Order Processing Service logs: order created.
- Notification Service logs: mock WhatsApp confirmation.
- PostgreSQL: row in `Orders` and `OrderItems` with parsed items (e.g. Pizza x2, Coke x1).

## Demo scenarios

### Normal flow

1. POST to `/webhook` with body like `docs/sample-webhook-payload.json` (message text: `Order: Pizza x2, Coke x1`).
2. Webhook returns 200 and publishes to RabbitMQ.
3. Order Processing Service consumes, parses, validates, saves order (idempotent by message ID), publishes `OrderProcessedEvent`.
4. Notification Service consumes and logs the mock confirmation.

### Failure scenario (worker down)

1. Stop Order Processing Service.
2. Send several POST requests to `/webhook` (e.g. multiple times with the same or different payloads).
3. Messages stay in `order-processing` (or retry queue).
4. Start Order Processing Service again: it consumes and processes backlog; orders appear in the database.

### Invalid message → DLQ

1. POST to `/webhook` with a message that does not match the order format (e.g. `"text": { "body": "Hello" }`).
2. Worker parses, validation fails; after retries (or immediately for parse/validation errors, depending on implementation) message is sent to `order-dlq`.
3. Check RabbitMQ management UI (`http://localhost:15672`) → Queues → `order-dlq` for the failed message.

## Configuration

- **WebhookService** `appsettings.json`: `Webhook:VerifyToken` (default `my-verify-token`), `ConnectionStrings:DefaultConnection`, `RabbitMQ`.
- **OrderProcessingService** / **NotificationService** `appsettings.json`: same `ConnectionStrings` and `RabbitMQ` (host, queues, `MaxRetryCount` = 3, `RetryDelayMs` = 30000).

Override with environment variables or other config as needed.

## Project structure

```
src/
  Shared.Contracts/     # OrderReceivedEvent, OrderProcessedEvent
  Domain/               # Order, OrderItem, FailedMessage, ProcessingLog
  Application/          # IOrderParser, IOrderValidator, IOrderService, repositories (ports)
  Infrastructure/       # EF Core, PostgreSQL, Repositories, RabbitMQ (publisher, retry, DLQ)
  WebhookService/       # ASP.NET Core API, webhook controller, handler
  OrderProcessingService/  # Worker, consumes order-processing, retry/DLQ logic
  NotificationService/  # Worker, consumes order-processed, mock WhatsApp
docs/
  sample-webhook-payload.json
  curl-examples.md
docker-compose.yml      # RabbitMQ + PostgreSQL
```

## Reliability and resilience

- **Retry with exponential backoff**: Retry queue uses per-message TTL: delay = `min(MaxRetryDelayMs, BaseRetryDelayMs * 2^retryCount)` (config: `RabbitMQ:BaseRetryDelayMs`, `MaxRetryDelayMs`, `RetryExponentialBackoff`).
- **Circuit breaker** (Order Processing Worker): Polly pipeline around order creation — after 5 consecutive failures the circuit opens for 30s; messages are requeued (Nack). Config: `CircuitBreaker:FailureThreshold`, `OpenCircuitDurationSeconds`, `RetryCount`.
- **DLQ**: Failed and invalid-format messages are stored in `order-dlq` and in `FailedMessages` table.
- **Idempotency**: Orders are keyed by `ExternalMessageId` (webhook message ID); duplicate webhook deliveries do not create duplicate orders.
- **Decoupling**: Webhook returns 200 after publishing to RabbitMQ; processing is asynchronous.
- **Health checks**: `/health`, `/health/ready`, `/health/live` for monitoring and orchestration.
- **Structured logging**: Correlation ID (header `X-Correlation-Id`), `MessageId`, `OrderId`, and scoped properties in Order Processing Worker logs.

## Architecture decisions and trade-offs

- **RabbitMQ**: Decouples webhook from processing, supports retry/DLQ with TTL and dead-letter exchanges, at-least-once delivery with acks.
- **Clean Architecture**: Domain and Application stay free of infrastructure; swapping DB or broker is easier; parsers and use cases are unit-testable.
- **Idempotency by MessageId**: Prevents duplicate orders when WhatsApp or the client retries the same webhook.
- **Trade-off**: At-least-once delivery requires idempotent handling; exactly-once would need extra mechanisms (e.g. outbox).

## License

MIT.
