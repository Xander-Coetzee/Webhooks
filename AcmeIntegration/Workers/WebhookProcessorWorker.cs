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
                                            OrderDate = DateTimeOffset.UtcNow,
                                            Status = "New",
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
                                        // UPDATE
                                        existingOrder.OrderTotal = acmeOrder.OrderTotal;
                                        existingOrder.Currency = acmeOrder.Currency;
                                        existingOrder.OrderNumber = acmeOrder.OrderNumber;

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

                                    // D. Mark successful
                                    webhook.Status = "Processed";
                                    await context.SaveChangesAsync(stoppingToken);

                                    // Update Run Metrics
                                    var currentRun = await context.ProcessingRuns.FindAsync(
                                        new object[] { runId },
                                        stoppingToken
                                    );
                                    if (currentRun != null)
                                    {
                                        currentRun.RecordsProcessed++;
                                        await context.SaveChangesAsync(stoppingToken);
                                    }

                                    _logger.LogInformation(
                                        "Successfully processed webhook {WebhookId} for Order {ExternalOrderId}",
                                        webhookId,
                                        webhook.ExternalOrderId
                                    );
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
