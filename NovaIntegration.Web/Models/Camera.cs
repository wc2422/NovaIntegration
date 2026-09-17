namespace NovaIntegration.Web.Models
{
    public class Camera
    {
        public int CameraId { get; set; }

        public string CameraName { get; set; } = string.Empty;

        public string Location { get; set; } = string.Empty;

        public string IpAddress { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public DateTime LastActivity { get; set; }

        public string StreamType { get; set; } = string.Empty;
    }
}