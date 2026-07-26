namespace NovaIntegration.Web.Models
{
    public class DashboardViewModel
    {
        public int TotalCameras { get; set; }

        public int ActiveCameras { get; set; }

        public int OfflineCameras { get; set; }

        public int AlertsToday { get; set; }

        public int DetectionsToday { get; set; }

        public string SystemStatus { get; set; } = string.Empty;

        public List<DashboardAlertViewModel> RecentAlerts { get; set; } = new();

        public List<CameraStatusViewModel> CameraStatuses { get; set; } = new();
    }

    public class DashboardAlertViewModel
    {
        public int AlertId { get; set; }

        public string CameraName { get; set; } = string.Empty;

        public string ObjectType { get; set; } = string.Empty;

        public string Severity { get; set; } = string.Empty;

        public double Confidence { get; set; }

        public DateTime DetectedAt { get; set; }
    }

    public class CameraStatusViewModel
    {
        public int CameraId { get; set; }

        public string CameraName { get; set; } = string.Empty;

        public string Location { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public DateTime LastActivity { get; set; }
    }
}