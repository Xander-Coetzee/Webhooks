namespace AcmeIntegration.Models
{
    // This represents the top-level order from the Acme API
    public class AcmeOrderResponse
    {
        public string ExternalOrderId { get; set; }
        public string OrderNumber { get; set; }
        public decimal OrderTotal { get; set; }
        public string Currency { get; set; }

        // This holds the list of items inside the order
        public List<AcmeOrderLineResponse> Lines { get; set; } = new List<AcmeOrderLineResponse>();
    }

    // This represents each individual item in the 'lines' array from the API
    public class AcmeOrderLineResponse
    {
        public string Sku { get; set; }
        public int Qty { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
