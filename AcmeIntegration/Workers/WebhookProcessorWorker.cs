using AcmeIntegration.Models;
using AcmeIntegration.Services;
using Microsoft.EntityFrameworkCore;

namespace AcmeIntegration.Workers
{
    public class WebhookProcessorWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly AcmeApiService _acmeService;

        public WebhookProcessorWorker(IServiceScopeFactory scopeFactory, AcmeApiService acmeService)
        {
            _scopeFactory = scopeFactory;
            _acmeService = acmeService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
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
                        // Logic for calling API and saving Order goes here
                    }
                }

                // Wait 30 seconds before checking again
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}
