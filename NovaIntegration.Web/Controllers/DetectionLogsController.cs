using Microsoft.AspNetCore.Mvc;
using NovaIntegration.Web.Models;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System;

namespace NovaIntegration.Web.Controllers
{
    public class DetectionLogsController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DetectionLogsController> _logger;

        public DetectionLogsController(
            IConfiguration configuration,
            ILogger<DetectionLogsController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public IActionResult Index()
        {
            List<Detection> detections = new List<Detection>();
            string? connectionString = _configuration.GetConnectionString("OracleConnection");
            bool databaseEnabled = _configuration.GetValue("Database:Enabled", true);

            if (databaseEnabled && !string.IsNullOrWhiteSpace(connectionString))
            {
                try
                {
                    using OracleConnection connection = new(connectionString);
                    connection.Open();
                    const string query = "SELECT EVENT_ID, CAMERA_ID, ALERT_TYPE, OBJECT_CLASS, CONFIDENCE_SCORE, TIMESTAMP FROM EVENTS ORDER BY TIMESTAMP DESC";

                    using OracleCommand command = new(query, connection);
                    using OracleDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        int eventId = reader.GetInt32(0);
                        double confidence = reader.GetDouble(4);

                        detections.Add(new Detection
                        {
                            DetectionId = eventId,
                            TrackingId = $"TRK-{DateTime.Now.Year}-{eventId}",
                            FrameId = $"FRM-CAM-{reader.GetInt32(1)}-{eventId}",
                            CameraName = "Camera " + reader.GetInt32(1),
                            CameraCode = "CAM-" + reader.GetInt32(1),
                            Location = "Milton Facility",
                            Zone = "Public Access Zone",
                            ObjectType = reader.GetString(3),
                            Confidence = confidence,
                            AlertType = reader.GetString(2),
                            Severity = confidence > 90 ? "Critical" : "Medium",
                            Status = "Pending",
                            AssignedOfficer = "Valmir Muratovski",
                            DetectedAt = reader.GetDateTime(5),
                            Image = "/images/detection-placeholder.png",
                            Notes = $"AI detection logged successfully with {confidence}% confidence.",
                            AiModel = "YOLOv8-Security",
                            ProcessingTimeMs = 24.5,
                            BoundingBoxCount = 1,
                            RequiresReview = true,
                            IsAcknowledged = false
                        });
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Oracle is unavailable. Loading an empty detection log in local demo mode.");
                }
            }

            return View(detections);
        }
    }
}
