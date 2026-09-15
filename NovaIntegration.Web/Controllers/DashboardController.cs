using Microsoft.AspNetCore.Mvc;
using NovaIntegration.Web.Models;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System;

namespace NovaIntegration.Web.Controllers
{
    public class DashboardController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(
            IConfiguration configuration,
            ILogger<DashboardController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public IActionResult Index()
        {
            DashboardViewModel model = new DashboardViewModel();
            model.RecentAlerts = new List<DashboardAlertViewModel>();
            model.TotalCameras = 1;
            model.ActiveCameras = 1;
            string? connectionString = _configuration.GetConnectionString("OracleConnection");
            bool databaseEnabled = _configuration.GetValue("Database:Enabled", true);

            if (databaseEnabled && !string.IsNullOrWhiteSpace(connectionString))
            {
                try
                {
                    using OracleConnection connection = new(connectionString);
                    connection.Open();

                    using (OracleCommand cmd = new OracleCommand("SELECT COUNT(*) FROM CAMERAS", connection))
                    {
                        var result = cmd.ExecuteScalar();
                        model.TotalCameras = result != DBNull.Value ? Convert.ToInt32(result) : 1;
                        model.ActiveCameras = model.TotalCameras;
                    }

                    using (OracleCommand cmd = new OracleCommand("SELECT COUNT(*) FROM EVENTS", connection))
                    {
                        var result = cmd.ExecuteScalar();
                        int count = result != DBNull.Value ? Convert.ToInt32(result) : 0;
                        model.AlertsToday = count;
                        model.DetectionsToday = count;
                    }

                    string query = "SELECT EVENT_ID, CAMERA_ID, ALERT_TYPE, OBJECT_CLASS, CONFIDENCE_SCORE, TIMESTAMP FROM (SELECT EVENT_ID, CAMERA_ID, ALERT_TYPE, OBJECT_CLASS, CONFIDENCE_SCORE, TIMESTAMP FROM EVENTS ORDER BY TIMESTAMP DESC) WHERE ROWNUM <= 5";

                    using (OracleCommand cmd = new OracleCommand(query, connection))
                    {
                        using OracleDataReader reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            double confidence = reader.GetDouble(4);
                            model.RecentAlerts.Add(new DashboardAlertViewModel
                            {
                                AlertId = reader.GetInt32(0),
                                CameraName = "Camera " + reader.GetInt32(1),
                                ObjectType = reader.GetString(3),
                                Severity = confidence > 90 ? "High" : "Medium",
                                Confidence = confidence,
                                DetectedAt = reader.GetDateTime(5)
                            });
                        }
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Oracle is unavailable. Loading the dashboard in local demo mode.");
                }
            }

            model.SystemStatus = "Online";
            return View(model);
        }
    }
}
