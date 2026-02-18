using System.ComponentModel.DataAnnotations;

namespace AcmeIntegration.Models
{
    public class ProcessingRun
    {
        public int Id { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }
        public int RecordsProcessed { get; set; }
        public int RecordsFailed { get; set; }
        public string Status { get; set; } // "Running", "Completed", "Failed"

        // We can link errors to this run if we want detailed error tracking
        public List<ProcessingError> Errors { get; set; } = new List<ProcessingError>();
    }

    public class ProcessingError
    {
        public int Id { get; set; }
        public int ProcessingRunId { get; set; }
        public string SourceSystem { get; set; }
        public string ExternalOrderId { get; set; }
        public string ErrorMessage { get; set; }
        public DateTimeOffset OccurredAt { get; set; }
    }
}
