using AcmeIntegration.Models;
using Microsoft.AspNetCore.Mvc;

namespace AcmeIntegration.Controllers
{
    // This controller simulates the external Acme API.
    [ApiController]
    [Route("external-api/orders")]
    public class ExternalMockController : ControllerBase
    {
        [HttpGet("{externalOrderId}")]
        public IActionResult GetOrder(string externalOrderId)
        {
            // Simulate API Failure
            if (externalOrderId.Contains("FAIL"))
            {
                return StatusCode(500, "Simulated External API Failure");
            }

            // Simulate randomized or static data based on ID
            var order = new AcmeOrderResponse
            {
                ExternalOrderId = externalOrderId,
                OrderNumber =
                    "SO-"
                    + (
                        externalOrderId.Contains("_")
                            ? externalOrderId.Split('_')[1]
                            : externalOrderId
                    ),
                OrderTotal = 150.00m,
                Currency = "USD",
                OrderDate = new DateTimeOffset(2026, 2, 18, 10, 0, 0, TimeSpan.Zero),
                Status = "SHIPPED",
                Customer = new AcmeCustomerResponse { Email = "integrated.customer@example.com" },
                Lines = new List<AcmeOrderLineResponse>
                {
                    new AcmeOrderLineResponse
                    {
                        Sku = "PROD-001",
                        Qty = externalOrderId.Contains("INVALID") ? -1 : 1, // Simulate Validation Error
                        UnitPrice = 150.00m,
                    },
                },
            };

            return Ok(order);
        }
    }
}
