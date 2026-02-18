using System.Net.Http.Json;
using AcmeIntegration.Models; // To access your DTOs

namespace AcmeIntegration.Services
{
    public class AcmeApiService
    {
        private readonly HttpClient _http;

        public AcmeApiService(HttpClient http)
        {
            _http = http;
        }

        public async Task<AcmeOrderResponse> GetOrderDetailsAsync(string orderId)
        {
            // In a real app, this would be:
            // return await _http.GetFromJsonAsync<AcmeOrderResponse>($"https://api.acme.com/orders/{orderId}");

            // For our simulation, let's just return a fake "Successful" response:
            return new AcmeOrderResponse
            {
                ExternalOrderId = orderId,
                OrderNumber = "SO-" + orderId.Split('_')[1],
                OrderTotal = 150.00m,
                Currency = "USD",
                OrderDate = DateTimeOffset.UtcNow.AddMinutes(-10),
                Status = "SHIPPED",
                Customer = new AcmeCustomerResponse { Email = "john.doe@example.com" },
                Lines = new List<AcmeOrderLineResponse>
                {
                    new AcmeOrderLineResponse
                    {
                        Sku = "PROD-001",
                        Qty = 1,
                        UnitPrice = 150.00m,
                    },
                },
            };
        }
    }
}
