using Microsoft.AspNetCore.Mvc;
using NovaIntegration.Web.Models;

namespace NovaIntegration.Web.Controllers
{
    public class AlertsController : Controller
    {
        public IActionResult Index()
        {
            List<Alert> alerts = new List<Alert>
            {
                new Alert
                {
                    AlertId = 1001,
                    CameraName = "Main Entrance Camera",
                    Location = "Building Entrance",
                    ObjectType = "Person",
                    AlertType = "Restricted Area",
                    Confidence = 96.4,
                    Severity = "High",
                    Status = "Open",
                    DetectedAt = DateTime.Now.AddMinutes(-4)
                },
                new Alert
                {
                    AlertId = 1002,
                    CameraName = "West Parking Lot",
                    Location = "Parking Area",
                    ObjectType = "Vehicle",
                    AlertType = "Boundary Crossing",
                    Confidence = 91.7,
                    Severity = "Medium",
                    Status = "Acknowledged",
                    DetectedAt = DateTime.Now.AddMinutes(-12)
                },
                new Alert
                {
                    AlertId = 1003,
                    CameraName = "Loading Area Camera",
                    Location = "Rear Entrance",
                    ObjectType = "Person",
                    AlertType = "After-Hours Activity",
                    Confidence = 87.2,
                    Severity = "Low",
                    Status = "Resolved",
                    DetectedAt = DateTime.Now.AddMinutes(-26)
                },
                new Alert
                {
                    AlertId = 1004,
                    CameraName = "Emergency Exit Camera",
                    Location = "East Exit",
                    ObjectType = "Person",
                    AlertType = "Emergency Exit Activity",
                    Confidence = 94.8,
                    Severity = "High",
                    Status = "Open",
                    DetectedAt = DateTime.Now.AddHours(-1)
                },
                new Alert
                {
                    AlertId = 1005,
                    CameraName = "Reception Camera",
                    Location = "Main Lobby",
                    ObjectType = "Package",
                    AlertType = "Unattended Object",
                    Confidence = 89.5,
                    Severity = "Medium",
                    Status = "Acknowledged",
                    DetectedAt = DateTime.Now.AddHours(-2)
                },
                new Alert
                {
                    AlertId = 1006,
                    CameraName = "Floor Two Hallway",
                    Location = "Second Floor",
                    ObjectType = "Person",
                    AlertType = "Movement Detected",
                    Confidence = 84.3,
                    Severity = "Low",
                    Status = "Resolved",
                    DetectedAt = DateTime.Now.AddHours(-3)
                }
            };

            return View(alerts);
        }
    }
}