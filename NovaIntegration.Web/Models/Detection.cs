namespace NovaIntegration.Web.Models;

public class Detection
{
    public int DetectionId { get; set; }

    public string TrackingId { get; set; } = string.Empty;

    public string FrameId { get; set; } = string.Empty;

    public string CameraName { get; set; } = string.Empty;

    public string CameraCode { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public string Zone { get; set; } = string.Empty;

    public string ObjectType { get; set; } = string.Empty;

    public double Confidence { get; set; }

    public string AlertType { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string AssignedOfficer { get; set; } = string.Empty;

    public DateTime DetectedAt { get; set; }

    public string Image { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public string AiModel { get; set; } = string.Empty;

    public double ProcessingTimeMs { get; set; }

    public int BoundingBoxCount { get; set; }

    public bool RequiresReview { get; set; }

    public bool IsAcknowledged { get; set; }
}