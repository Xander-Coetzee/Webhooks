using AcmeIntegration.Data;
using AcmeIntegration.Models;
using Microsoft.AspNetCore.Mvc;

namespace AcmeIntegration.Controllers
{
    [ApiController]
    [Route("webhooks/orders")]
    public class WebhooksController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public WebhooksController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveOrder([FromBody] WebhookEvent payload)
        {
            payload.Status = "Pending";
            
            _dbContext.WebhookEvents.Add(payload);
            await _dbContext.SaveChangesAsync();
            
            return Accepted();
        }
    }
}