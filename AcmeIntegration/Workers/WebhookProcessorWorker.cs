using AcmeIntegration.Data;
using AcmeIntegration.Models;
using AcmeIntegration.Services;
using Microsoft.EntityFrameworkCore;

namespace AcmeIntegration.Workers
{
    public class WebhookProcessorWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly AcmeApiService _acmeService;
        private readonly ILogger<WebhookProcessorWorker> _logger;

        public WebhookProcessorWorker(
            IServiceScopeFactory scopeFactory,
            AcmeApiService acmeService,
            ILogger<WebhookProcessorWorker> logger
        )
        {
            _scopeFactory = scopeFactory;
            _acmeService = acmeService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    List<int> pendingWebhookIds;

                    // 1. Get the pending webhook IDs using a short-lived scope
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        pendingWebhookIds = await context
                            .WebhookEvents.Where(w => w.Status == "Pending")
                            .Select(w => w.Id)
                            .ToListAsync(stoppingToken);
                    }

                    // 2. If we have work to do, create a "Processing Run"
                    if (pendingWebhookIds.Any())
                    {
                        int runId;
                        using (var scope = _scopeFactory.CreateScope())
                        {
                            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                            var run = new ProcessingRun
                            {
                                StartTime = DateTimeOffset.UtcNow,
                                Status = "Running",
                                RecordsProcessed = 0,
                                RecordsFailed = 0,
                                RecordsSkipped = 0,
                            };
                            context.ProcessingRuns.Add(run);
                            await context.SaveChangesAsync(stoppingToken);
                            runId = run.Id;
                        }

                        // 3. Process each webhook in its own isolated scope
                        foreach (var webhookId in pendingWebhookIds)
                        {
                            using (var scope = _scopeFactory.CreateScope())
                            {
                                var context =
                                    scope.ServiceProvider.GetRequiredService<AppDbContext>();

                                // Re-fetch the webhook to track it in this new context
                                var webhook = await context.WebhookEvents.FindAsync(
                                    new object[] { webhookId },
                                    stoppingToken
                                );
                                if (webhook == null)
                                    continue;

                                try
                                {
                                    // A. Fetch details from Acme API
                                    var acmeOrder = await _acmeService.GetOrderDetailsAsync(
                                        webhook.ExternalOrderId
                                    );

                                    // --- VALIDATION LOGIC ---
                                    ValidateOrder(acmeOrder);

                                    // B. Check if we already have this order
                                    var existingOrder = await context
                                        .Orders.Include(o => o.Lines)
                                        .Where(o =>
                                            o.ExternalOrderId == acmeOrder.ExternalOrderId
                                            && o.SourceSystem == "Acme"
                                        )
                                        .FirstOrDefaultAsync(stoppingToken);

                                    bool wasSkipped = false;

                                    // C. Upsert Logic (Update or Insert)
                                    if (existingOrder == null)
                                    {
                                        // INSERT
                                        var newOrder = new Order
                                        {
                                            SourceSystem = "Acme",
                                            ExternalOrderId = acmeOrder.ExternalOrderId,
                                            OrderNumber = acmeOrder.OrderNumber,
                                            OrderTotal = acmeOrder.OrderTotal,
                                            Currency = acmeOrder.Currency,
                                            OrderDate = acmeOrder.OrderDate,
                                            Status = acmeOrder.Status,
                                            CustomerEmail = acmeOrder.Customer?.Email,
                                            Lines = acmeOrder
                                                .Lines.Select(l => new OrderLine
                                                {
                                                    Sku = l.Sku,
                                                    Qty = l.Qty,
                                                    UnitPrice = l.UnitPrice,
                                                })
                                                .ToList(),
                                        };
                                        context.Orders.Add(newOrder);
                                    }
                                    else
                                    {
                                        // CHECK FOR CHANGES (Idempotency Optimization)
                                        // If identical -> skip
                                        if (IsOrderIdentical(existingOrder, acmeOrder))
                                        {
                                            wasSkipped = true;
                                            _logger.LogInformation(
                                                "Order {ExternalOrderId} is identical. Skipping update.",
                                                acmeOrder.ExternalOrderId
                                            );
                                        }
                                        else
                                        {
                                            // UPDATE
                                            existingOrder.OrderTotal = acmeOrder.OrderTotal;
                                            existingOrder.Currency = acmeOrder.Currency;
                                            existingOrder.OrderNumber = acmeOrder.OrderNumber;
                                            existingOrder.OrderDate = acmeOrder.OrderDate;
                                            existingOrder.Status = acmeOrder.Status;
                                            existingOrder.CustomerEmail = acmeOrder.Customer?.Email;

                                            context.OrderLines.RemoveRange(existingOrder.Lines);
                                            existingOrder.Lines = acmeOrder
                                                .Lines.Select(l => new OrderLine
                                                {
                                                    Sku = l.Sku,
                                                    Qty = l.Qty,
                                                    UnitPrice = l.UnitPrice,
                                                })
                                                .ToList();
                                        }
                                    }

                                    // D. Mark successful (even if skipped, the webhook event itself is "processed")
                                    webhook.Status = "Processed";
                                    await context.SaveChangesAsync(stoppingToken);

                                    // Update Run Metrics
                                    var currentRun = await context.ProcessingRuns.FindAsync(
                                        new object[] { runId },
                                        stoppingToken
                                    );
                                    if (currentRun != null)
                                    {
                                        if (wasSkipped)
                                            currentRun.RecordsSkipped++;
                                        else
                                            currentRun.RecordsProcessed++;

                                        await context.SaveChangesAsync(stoppingToken);
                                    }

                                    if (!wasSkipped)
                                    {
                                        _logger.LogInformation(
                                            "Successfully processed webhook {WebhookId} for Order {ExternalOrderId}",
                                            webhookId,
                                            webhook.ExternalOrderId
                                        );
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(
                                        ex,
                                        "Error processing webhook {WebhookId}",
                                        webhook.Id
                                    );
                                    webhook.Status = "Failed";
                                    webhook.LastError = ex.Message;

                                    // Record Error in ProcessingErrors table
                                    context.ProcessingErrors.Add(
                                        new ProcessingError
                                        {
                                            ProcessingRunId = runId,
                                            SourceSystem = webhook.SourceSystem,
                                            ExternalOrderId = webhook.ExternalOrderId,
                                            ErrorMessage = ex.Message,
                                            OccurredAt = DateTimeOffset.UtcNow,
                                        }
                                    );

                                    // Update Run Metrics
                                    var currentRun = await context.ProcessingRuns.FindAsync(
                                        new object[] { runId },
                                        stoppingToken
                                    );
                                    if (currentRun != null)
                                    {
                                        currentRun.RecordsFailed++;
                                    }

                                    await context.SaveChangesAsync(stoppingToken);
                                }
                            }
                        }

                        // 4. Mark Run as Completed
                        using (var scope = _scopeFactory.CreateScope())
                        {
                            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                            var run = await context.ProcessingRuns.FindAsync(
                                new object[] { runId },
                                stoppingToken
                            );
                            if (run != null)
                            {
                                run.Status = "Completed";
                                run.EndTime = DateTimeOffset.UtcNow;
                                await context.SaveChangesAsync(stoppingToken);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fatal error in WebhookProcessorWorker outer loop");
                }

                // Wait 30 seconds before checking again
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        private bool IsOrderIdentical(Order existing, AcmeOrderResponse incoming)
        {
            // 1. Check Header Fields
            if (existing.OrderTotal != incoming.OrderTotal)
                return false;
            if (existing.Currency != incoming.Currency)
                return false;
            // Note: We might normally ignore OrderNumber changes if ExternalID matches, but let's check it.
            if (existing.OrderNumber != incoming.OrderNumber)
                return false;
            if (existing.Status != incoming.Status)
                return false;
            if (existing.CustomerEmail != incoming.Customer?.Email)
                return false;
            // Date comparison can be tricky with offsets, but let's try exact match
            if (existing.OrderDate != incoming.OrderDate)
                return false;

            // 2. Check Lines
            if (existing.Lines.Count != incoming.Lines.Count)
                return false;

            // Simple check: Sort both by SKU and compare
            var existingLines = existing.Lines.OrderBy(l => l.Sku).ToList();
            var incomingLines = incoming.Lines.OrderBy(l => l.Sku).ToList();

            for (int i = 0; i < existingLines.Count; i++)
            {
                if (existingLines[i].Sku != incomingLines[i].Sku)
                    return false;
                if (existingLines[i].Qty != incomingLines[i].Qty)
                    return false;
                if (existingLines[i].UnitPrice != incomingLines[i].UnitPrice)
                    return false;
            }

            return true;
        }

        private void ValidateOrder(AcmeOrderResponse order)
        {
            // Required: sourceSystem (implied "Acme"), externalOrderId, orderNumber, currency, at least 1 line
            if (string.IsNullOrWhiteSpace(order.ExternalOrderId))
                throw new InvalidOperationException(
                    "Validation Error: ExternalOrderId is missing."
                );

            if (string.IsNullOrWhiteSpace(order.OrderNumber))
                throw new InvalidOperationException("Validation Error: OrderNumber is missing.");

            if (string.IsNullOrWhiteSpace(order.Currency))
                throw new InvalidOperationException("Validation Error: Currency is missing.");

            if (order.Lines == null || !order.Lines.Any())
                throw new InvalidOperationException(
                    "Validation Error: Order must have at least 1 line."
                );

            // Qty must be > 0, UnitPrice must be >= 0, SKU cannot be empty
            foreach (var line in order.Lines)
            {
                if (string.IsNullOrWhiteSpace(line.Sku))
                    throw new InvalidOperationException(
                        "Validation Error: Line item SKU cannot be empty."
                    );

                if (line.Qty <= 0)
                    throw new InvalidOperationException(
                        $"Validation Error: SKU {line.Sku} has invalid Qty ({line.Qty}). Must be > 0."
                    );

                if (line.UnitPrice < 0)
                    throw new InvalidOperationException(
                        $"Validation Error: SKU {line.Sku} has invalid UnitPrice ({line.UnitPrice}). Must be >= 0."
                    );
            }
        }
    }
}
