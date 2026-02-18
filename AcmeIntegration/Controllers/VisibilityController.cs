using AcmeIntegration.Data;
using AcmeIntegration.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcmeIntegration.Controllers
{
    [ApiController]
    [Route("api/import-runs")]
    public class VisibilityController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public VisibilityController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // GET /api/import-runs
        // List last 20 processing runs (or batches) with counts and status.
        [HttpGet]
        public async Task<IActionResult> GetImportRuns()
        {
            var runs = await _dbContext
                .ProcessingRuns.OrderByDescending(r => r.StartTime)
                .Take(20)
                .ToListAsync();

            return Ok(runs);
        }

        // GET /api/import-runs/{id}
        // Show run details + errors/warnings (top 20 is fine).
        [HttpGet("{id}")]
        public async Task<IActionResult> GetImportRunDetails(int id)
        {
            var run = await _dbContext
                .ProcessingRuns.Include(r => r.Errors.OrderBy(e => e.OccurredAt).Take(20))
                .FirstOrDefaultAsync(r => r.Id == id);

            if (run == null)
            {
                return NotFound();
            }

            return Ok(run);
        }
    }
}
