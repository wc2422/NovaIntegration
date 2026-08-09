using Microsoft.AspNetCore.Mvc;
using NovaIntegration.Web.Models;

namespace NovaIntegration.Web.Controllers
{
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            DashboardViewModel model = new DashboardViewModel
            {
                TotalCameras = 8,
                ActiveCameras = 6,
                OfflineCameras = 2,
                AlertsToday = 12,
                DetectionsToday = 47,
                SystemStatus = "Operational",

                RecentAlerts = new List<DashboardAlertViewModel>
                {
                    new DashboardAlertViewModel
                    {
                        AlertId = 1001,
                        CameraName = "Main Entrance",
                        ObjectType = "Person",
                        Severity = "High",
                        Confidence = 96.4,
                        DetectedAt = DateTime.Now.AddMinutes(-4)
                    },
                    new DashboardAlertViewModel
                    {
                        AlertId = 1002,
                        CameraName = "West Parking Lot",
                        ObjectType = "Vehicle",
                        Severity = "Medium",
                        Confidence = 91.7,
                        DetectedAt = DateTime.Now.AddMinutes(-12)
                    },
                    new DashboardAlertViewModel
                    {
                        AlertId = 1003,
                        CameraName = "Loading Area",
                        ObjectType = "Person",
                        Severity = "Low",
                        Confidence = 87.2,
                        DetectedAt = DateTime.Now.AddMinutes(-26)
                    }
                },

                CameraStatuses = new List<CameraStatusViewModel>
                {
                    new CameraStatusViewModel
                    {
                        CameraId = 1,
                        CameraName = "Main Entrance",
                        Location = "Building Entrance",
                        Status = "Online",
                        LastActivity = DateTime.Now.AddMinutes(-1)
                    },
                    new CameraStatusViewModel
                    {
                        CameraId = 2,
                        CameraName = "West Parking Lot",
                        Location = "Parking Area",
                        Status = "Online",
                        LastActivity = DateTime.Now.AddMinutes(-3)
                    },
                    new CameraStatusViewModel
                    {
                        CameraId = 3,
                        CameraName = "Loading Area",
                        Location = "Rear Entrance",
                        Status = "Offline",
                        LastActivity = DateTime.Now.AddHours(-2)
                    },
                    new CameraStatusViewModel
                    {
                        CameraId = 4,
                        CameraName = "Floor Two Hallway",
                        Location = "Second Floor",
                        Status = "Online",
                        LastActivity = DateTime.Now.AddMinutes(-5)
                    }
                }
            };

            return View(model);
        }
    }
}