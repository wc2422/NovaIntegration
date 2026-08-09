using Microsoft.AspNetCore.Mvc;
using NovaIntegration.Web.Models;

namespace NovaIntegration.Web.Controllers;

public class DetectionLogsController : Controller
{
    public IActionResult Index()
    {
        string[] cameraNames =
        {
            "Main Entrance Camera",
            "West Parking Camera",
            "Loading Dock Camera",
            "Reception Camera",
            "Emergency Exit Camera",
            "Floor Two Hallway",
            "North Stairwell Camera",
            "Warehouse Camera"
        };

        string[] cameraCodes =
        {
            "CAM-001",
            "CAM-002",
            "CAM-003",
            "CAM-004",
            "CAM-005",
            "CAM-006",
            "CAM-007",
            "CAM-008"
        };

        string[] locations =
        {
            "Main Entrance",
            "West Parking Lot",
            "Loading Dock",
            "Reception Lobby",
            "East Emergency Exit",
            "Second Floor Hallway",
            "North Stairwell",
            "Warehouse Zone A"
        };

        string[] objectTypes =
        {
            "Person",
            "Vehicle",
            "Package",
            "Person",
            "Person",
            "Person",
            "Package",
            "Vehicle"
        };

        string[] alertTypes =
        {
            "Restricted Area Entry",
            "Boundary Crossing",
            "Unattended Object",
            "Loitering Detected",
            "Emergency Exit Activity",
            "After-Hours Activity",
            "Unknown Object",
            "Motion Detected"
        };

        string[] severities =
        {
            "Critical",
            "High",
            "Medium",
            "Low"
        };

        string[] statuses =
        {
            "Pending",
            "Reviewed",
            "Escalated",
            "Resolved"
        };

        string[] officers =
        {
            "Nipun Barot",
            "Alex Yu",
            "Valmir Muratovski",
            "Abdullah Muhammad",
            "Unassigned"
        };

        List<Detection> detections = new();

        for (int index = 0; index < 48; index++)
        {
            int cameraIndex = index % cameraNames.Length;
            int severityIndex = index % severities.Length;
            int statusIndex = index % statuses.Length;
            int officerIndex = index % officers.Length;

            detections.Add(new Detection
            {
                DetectionId = 3001 + index,

                TrackingId =
                    $"TRK-{DateTime.Now.Year}-{8001 + index}",

                FrameId =
                    $"FRM-{cameraCodes[cameraIndex]}-{125000 + index}",

                CameraName = cameraNames[cameraIndex],

                CameraCode = cameraCodes[cameraIndex],

                Location = locations[cameraIndex],

                Zone = GetCameraZone(cameraIndex),

                ObjectType = objectTypes[cameraIndex],

                Confidence = Math.Round(
                    78.5 + ((index * 3.7) % 21),
                    1
                ),

                AlertType = alertTypes[cameraIndex],

                Severity = severities[severityIndex],

                Status = statuses[statusIndex],

                AssignedOfficer = officers[officerIndex],

                DetectedAt = DateTime.Now.AddMinutes(
                    -(index * 17 + 3)
                ),

                Image = "/images/detection-placeholder.png",

                Notes = GetDetectionNotes(
                    alertTypes[cameraIndex],
                    locations[cameraIndex]
                ),

                AiModel = GetAiModel(index),

                ProcessingTimeMs = Math.Round(
                    18 + ((index * 4.6) % 38),
                    1
                ),

                BoundingBoxCount = 1 + index % 4,

                RequiresReview =
                    statuses[statusIndex] == "Pending" ||
                    statuses[statusIndex] == "Escalated",

                IsAcknowledged =
                    statuses[statusIndex] == "Reviewed" ||
                    statuses[statusIndex] == "Resolved"
            });
        }

        return View(detections);
    }

    private static string GetCameraZone(int cameraIndex)
    {
        return cameraIndex switch
        {
            0 => "Public Access Zone",
            1 => "Vehicle Monitoring Zone",
            2 => "Restricted Operations Zone",
            3 => "Visitor Management Zone",
            4 => "Emergency Access Zone",
            5 => "Employee Access Zone",
            6 => "Restricted Stairwell Zone",
            _ => "Asset Protection Zone"
        };
    }

    private static string GetAiModel(int index)
    {
        return (index % 3) switch
        {
            0 => "YOLOv8-Security",
            1 => "YOLOv8-Edge",
            _ => "NovaVision-1.4"
        };
    }

    private static string GetDetectionNotes(
        string alertType,
        string location)
    {
        return alertType switch
        {
            "Restricted Area Entry" =>
                $"An individual entered a restricted area near {location}. Security review is recommended.",

            "Boundary Crossing" =>
                $"A monitored boundary was crossed at {location}. The event was recorded for investigation.",

            "Unattended Object" =>
                $"An object remained unattended at {location} beyond the configured monitoring threshold.",

            "Loitering Detected" =>
                $"A person remained within {location} longer than the configured loitering period.",

            "Emergency Exit Activity" =>
                $"Activity was detected around the emergency exit at {location}.",

            "After-Hours Activity" =>
                $"Movement was detected at {location} outside normal operating hours.",

            "Unknown Object" =>
                $"The AI model detected an unidentified object at {location}.",

            _ =>
                $"Motion was detected and recorded by the camera monitoring {location}."
        };
    }
}