using AcmeIntegration.Models;
using Microsoft.EntityFrameworkCore;

namespace AcmeIntegration.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        // This tells the database to create a table called WebhookEvents based on our class
        public DbSet<WebhookEvent> WebhookEvents { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderLine> OrderLines { get; set; }
    }
}
