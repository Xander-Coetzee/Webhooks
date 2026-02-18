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
            var response = await _http.GetAsync($"/external-api/orders/{orderId}");
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<AcmeOrderResponse>();
        }
    }
}
