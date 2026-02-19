# Acme Webhook Integration

This project implements a webhook ingestion service for the fictional Acme Commerce platform. It receives `order.created` webhooks, fetches full order details from an external API, and stores them in a local SQLite database using an idempotent upsert strategy.

### Prerequisites

- .NET 10.0 SDK or later
- PowerShell or Terminal (for running curl/Invoke-RestMethod)

### Setup & Run

1.  **Navigate to the project directory:**

    ```bash
    cd src/Webhooks/AcmeIntegration
    ```

2.  **Restore dependencies and build:**

    ```bash
    dotnet build
    ```

3.  **Apply Database Migrations:**
    This will create the SQLite database file (`acmecommerce.db`) and apply the necessary schema.

    ```bash
    dotnet ef database update
    ```

4.  **Run the application:**
    ```bash
    dotnet run
    ```
    The API will start at `http://localhost:5287` (or similar, check console output).

### 1. Send a Webhook (PowerShell)

Simulate an incoming webhook from Acme using PowerShell:

```powershell
Invoke-RestMethod -Uri "http://localhost:5287/webhooks/orders" -Method Post -Body (@{
    eventId = "evt_test_001"
    eventType = "order.created"
    sourceSystem = "Acme"
    externalOrderId = "ORD-TEST-001"
    occurredAt = "2026-02-18T10:00:00Z"
} | ConvertTo-Json) -ContentType "application/json"
```

### 2. Verify Order Processing

After a few seconds (the worker runs every 30s), check if the order was created:

```powershell
Invoke-RestMethod -Uri "http://localhost:5287/api/orders/SO-TEST-001" -Method Get
```

_Note: The mock API service generates `SO-{externalOrderId_suffix}` as the Order Number._

### 3. Check Visibility Metrics

View the processing run history and any errors:

```powershell
Invoke-RestMethod -Uri "http://localhost:5287/api/import-runs" -Method Get
```

## 🏗 Architecture & Design

### Components

- **Webhook Endpoint (`WebhooksController`)**: Receives JSON payloads, validates basic fields (Id, Type, Source), and stores them in the `WebhookEvents` table with status `Pending`. Returns `202 Accepted` immediately.
- **Background Worker (`WebhookProcessorWorker`)**: A hosted service that runs every 30 seconds.
  - Polls for `Pending` webhooks.
  - Calls the `AcmeApiService` (mocked) to get full order details.
  - Performs **Idempotent Upsert**: Checks if the order exists by `ExternalOrderId` + `SourceSystem`.
    - If new: Inserts order + lines.
    - If exists: Updates header fields and replaces lines.
  - Validates business rules (Qty > 0, etc.).
  - Tracks execution metrics in `ProcessingRuns` and errors in `ProcessingErrors`.
- **Database**: SQLite via Entity Framework Core.
  - `WebhookEvents`: Raw ingestion log.
  - `Orders` / `OrderLines`: Domain entities.
  - `ProcessingRuns` / `ProcessingErrors`: Visibility and monitoring.

### Assumptions & Tradeoffs

1.  **Database**: Used SQLite for simplicity and portability (zero configuration).
2.  **Worker Interval**: Hardcoded to 30s. In production, this would be configurable or event-driven.
3.  **Mock API**: The `AcmeApiService` mocks the external call and returns static/randomized data for demonstration purposes.
4.  **Error Handling**: Failed webhooks are marked as `Failed` and not retried automatically. A production system would implement exponential backoff retry logic.

### Future Improvements

1.  **Resiliency**: Implement proper retry policies (Polly) for the external API calls.
2.  **Queueing**: Replace the database-polling worker with a true message queue (e.g., Azure Service Bus) for better scalability and immediate processing.
3.  **Security**: Add API Key authentication for the webhook endpoint to ensure calls typically come from Acme.
4.  **Testing**: Add unit tests for the Worker logic and integration tests for the full flow.
5.  **Configuration**: Move magic strings and intervals to `appsettings.json`.
