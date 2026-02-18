using AcmeIntegration.Data;
using AcmeIntegration.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcmeIntegration.Controllers
{
    [ApiController]
    [Route("api/orders")]
    public class OrdersController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public OrdersController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("{orderNumber}")] // This captures /api/orders/SO-10001
        public async Task<IActionResult> GetOrder(string orderNumber)
        {
            var order = await _dbContext
                .Orders.Include(o => o.Lines) // Critical: fetch the lines too!
                .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);
            if (order == null)
                return NotFound();
            return Ok(order);
        }
    }
}
