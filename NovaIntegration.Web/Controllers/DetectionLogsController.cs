using Microsoft.AspNetCore.Mvc;
using NovaIntegration.Web.Models;

namespace NovaIntegration.Web.Controllers;

public class DetectionLogsController : Controller
{
    public IActionResult Index()
    {
        List<Detection> detections = new()
        {
            new Detection
            {
                DetectionId = 2001,
                CameraName = "Main Entrance Camera",
                Location = "Building Entrance",
                ObjectType = "Person",
                Confidence = 96.4,
                AlertType = "Restricted Area",
                DetectedAt = DateTime.Now.AddMinutes(-4),
                Image = "/images/detection-placeholder.png"
            },

            new Detection
            {
                DetectionId = 2002,
                CameraName = "West Parking Lot",
                Location = "Parking Area",
                ObjectType = "Vehicle",
                Confidence = 91.7,
                AlertType = "Boundary Crossing",
                DetectedAt = DateTime.Now.AddMinutes(-15),
                Image = "/images/detection-placeholder.png"
            },

            new Detection
            {
                DetectionId = 2003,
                CameraName = "Loading Area Camera",
                Location = "Rear Entrance",
                ObjectType = "Person",
                Confidence = 87.2,
                AlertType = "After-Hours Activity",
                DetectedAt = DateTime.Now.AddMinutes(-38),
                Image = "/images/detection-placeholder.png"
            },

            new Detection
            {
                DetectionId = 2004,
                CameraName = "Reception Camera",
                Location = "Main Lobby",
                ObjectType = "Package",
                Confidence = 89.5,
                AlertType = "Unattended Object",
                DetectedAt = DateTime.Now.AddHours(-2),
                Image = "/images/detection-placeholder.png"
            },

            new Detection
            {
                DetectionId = 2005,
                CameraName = "Emergency Exit Camera",
                Location = "East Exit",
                ObjectType = "Person",
                Confidence = 94.8,
                AlertType = "Emergency Exit Activity",
                DetectedAt = DateTime.Now.AddHours(-3),
                Image = "/images/detection-placeholder.png"
            }
        };

        return View(detections);
    }
}