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
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                        // 1. Get the pending webhooks
                        var pendingWebhooks = await context
                            .WebhookEvents.Where(w => w.Status == "Pending")
                            .ToListAsync(stoppingToken);

                        // 2. Process each one
                        foreach (var webhook in pendingWebhooks)
                        {
                            try
                            {
                                // 1. Fetch details from Acme API using our injected service
                                var acmeOrder = await _acmeService.GetOrderDetailsAsync(
                                    webhook.ExternalOrderId
                                );

                                // 2. Map to our database Order model
                                var newOrder = new Order
                                {
                                    SourceSystem = "Acme",
                                    ExternalOrderId = acmeOrder.ExternalOrderId,
                                    OrderNumber = acmeOrder.OrderNumber,
                                    OrderTotal = acmeOrder.OrderTotal,
                                    Currency = acmeOrder.Currency,
                                    OrderDate = DateTimeOffset.UtcNow,
                                    Status = "New",
                                };

                                // 3. Map the items (Lines)
                                foreach (var line in acmeOrder.Lines)
                                {
                                    newOrder.Lines.Add(
                                        new OrderLine
                                        {
                                            Sku = line.Sku,
                                            Qty = line.Qty,
                                            UnitPrice = line.UnitPrice,
                                        }
                                    );
                                }

                                // 4. Save to DB and update Webhook status to prevent duplicate processing
                                context.Orders.Add(newOrder);
                                webhook.Status = "Processed";

                                await context.SaveChangesAsync();
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
                                await context.SaveChangesAsync();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fatal error in WebhookProcessorWorker loop");
                }

                // Wait 30 seconds before checking again
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}
