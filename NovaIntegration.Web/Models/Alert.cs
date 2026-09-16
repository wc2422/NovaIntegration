namespace NovaIntegration.Web.Models
{
    public class Alert
    {
        public int AlertId { get; set; }

        public string CameraName { get; set; } = string.Empty;

        public string Location { get; set; } = string.Empty;

        public string ObjectType { get; set; } = string.Empty;

        public string AlertType { get; set; } = string.Empty;

        public double Confidence { get; set; }

        public string Severity { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public DateTime DetectedAt { get; set; }
    }
}