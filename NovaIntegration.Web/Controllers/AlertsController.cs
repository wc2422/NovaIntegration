using Microsoft.AspNetCore.Mvc;
using NovaIntegration.Web.Models;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System;

namespace NovaIntegration.Web.Controllers
{
    public class AlertsController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AlertsController> _logger;

        public AlertsController(
            IConfiguration configuration,
            ILogger<AlertsController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public IActionResult Index()
        {
            List<Alert> alerts = new List<Alert>();
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
                        alerts.Add(new Alert
                        {
                            AlertId = reader.GetInt32(0),
                            CameraName = "Camera " + reader.GetInt32(1),
                            Location = "Milton Facility",
                            ObjectType = reader.GetString(3),
                            AlertType = reader.GetString(2),
                            Confidence = reader.GetDouble(4),
                            Severity = reader.GetDouble(4) > 90 ? "High" : "Medium",
                            Status = "Open",
                            DetectedAt = reader.GetDateTime(5)
                        });
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Oracle is unavailable. Loading an empty alerts view in local demo mode.");
                }
            }

            return View(alerts);
        }
    }
}
