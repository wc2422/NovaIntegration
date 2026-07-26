namespace NovaIntegration.Web.Models;

public class Detection
{
    public int DetectionId { get; set; }

    public string CameraName { get; set; } = "";

    public string Location { get; set; } = "";

    public string ObjectType { get; set; } = "";

    public double Confidence { get; set; }

    public string AlertType { get; set; } = "";

    public DateTime DetectedAt { get; set; }

    public string Image { get; set; } = "";
}