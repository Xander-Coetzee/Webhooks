using System.ComponentModel.DataAnnotations;

namespace AcmeIntegration.Models
{
    public class WebhookEvent
    {
        public int Id { get; set; }
        public string EventId { get; set; }
        public string EventType { get; set; }
        
        [Required]
        public string SourceSystem { get; set; }
        
        [Required]
        public string ExternalOrderId { get; set; }
        
        public DateTimeOffset OccurredAt { get; set; }
        public string? Payload { get; set; }
        public string? Status { get; set; }
        public int Attempts { get; set; }
        public string? LastError { get; set; }
    }
}