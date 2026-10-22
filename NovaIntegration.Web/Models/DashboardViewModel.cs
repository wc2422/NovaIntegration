namespace NovaIntegration.Web.Models
{
    public class DashboardViewModel
    {
        public int TotalCameras { get; set; }

        public int ActiveCameras { get; set; }

        public int OfflineCameras { get; set; }

        public int AlertsToday { get; set; }

        public int DetectionsToday { get; set; }

        public double AverageConfidence { get; set; }

        public string SystemStatus { get; set; } = string.Empty;

        public bool DatabaseConnected { get; set; }

        public double DatabaseAvailability { get; set; }

        public DateTime LastCheckedAt { get; set; }

        public int RangeHours { get; set; } = 12;

        public List<DashboardAlertViewModel> RecentAlerts { get; set; } = new();

        public List<CameraStatusViewModel> CameraStatuses { get; set; } = new();

        public List<DashboardEventTypeCount> EventTypeCounts { get; set; } = new();

        public List<DashboardActivityPoint> ActivityPoints { get; set; } = new();
    }

    public class DashboardEventTypeCount
    {
        public string EventType { get; set; } = string.Empty;

        public int Count { get; set; }
    }

    public class DashboardActivityPoint
    {
        public DateTime Hour { get; set; }

        public string Label { get; set; } = string.Empty;

        public int Detections { get; set; }

        public int Alerts { get; set; }

        public int HighConfidence { get; set; }
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
