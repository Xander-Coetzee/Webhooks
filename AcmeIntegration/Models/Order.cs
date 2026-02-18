namespace AcmeIntegration.Models
{
    public class Order
    {
        public int Id { get; set; }
        public string SourceSystem { get; set; }
        public string ExternalOrderId { get; set; }
        public string OrderNumber { get; set; }
        public DateTimeOffset OrderDate { get; set; }
        public string Currency { get; set; }
        public string Status { get; set; }
        public decimal OrderTotal { get; set; }
        public string CustomerEmail { get; set; }

        // This links the order to its specific items
        public List<OrderLine> Lines { get; set; } = new List<OrderLine>();
    }

    public class OrderLine
    {
        public int Id { get; set; }
        public int OrderId { get; set; } // Foreign key linking back to the Order
        public string Sku { get; set; }
        public int Qty { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
