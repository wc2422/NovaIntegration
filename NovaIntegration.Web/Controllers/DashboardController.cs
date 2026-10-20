using Microsoft.AspNetCore.Mvc;
using NovaIntegration.Web.Models;
using Oracle.ManagedDataAccess.Client;

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

        public IActionResult Index(int rangeHours = 12)
        {
            rangeHours = rangeHours is 12 or 24 or 48 ? rangeHours : 12;
            DashboardViewModel model = new()
            {
                RangeHours = rangeHours,
                SystemStatus = "Database unavailable",
                LastCheckedAt = DateTime.Now
            };

            bool databaseEnabled = _configuration.GetValue("Database:Enabled", true);
            string? connectionString = _configuration.GetConnectionString("OracleConnection")
                ?? _configuration.GetConnectionString("OracleDbConnection");
            if (!databaseEnabled || string.IsNullOrWhiteSpace(connectionString))
            {
                model.SystemStatus = databaseEnabled
                    ? "Database not configured"
                    : "Database disabled";
                model.ActivityPoints = CreateEmptyActivityPoints(rangeHours);
                return View(model);
            }

            try
            {
                using OracleConnection connection = new(connectionString);
                connection.Open();

                LoadCameras(connection, model);
                LoadTodaySummary(connection, model);
                LoadEventTypeCounts(connection, model);
                LoadActivity(connection, model, rangeHours);
                LoadRecentEvents(connection, model);

                model.DatabaseConnected = true;
                model.SystemStatus = "Database connected";
                model.DatabaseAvailability = 100;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Oracle is unavailable. Dashboard values are shown as zero.");
                ResetDatabaseValues(model);
                model.SystemStatus = "Database unavailable";
                model.DatabaseConnected = false;
                model.DatabaseAvailability = 0;
            }

            model.LastCheckedAt = DateTime.Now;
            return View(model);
        }

        private static void LoadCameras(OracleConnection connection, DashboardViewModel model)
        {
            const string query = "SELECT CAMERA_ID, LOCATION_NAME, STATUS FROM CAMERAS ORDER BY CAMERA_ID";
            using OracleCommand command = new(query, connection);
            using OracleDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                string status = reader.IsDBNull(2) ? "Unknown" : reader.GetString(2).Trim();
                CameraStatusViewModel camera = new()
                {
                    CameraId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                    CameraName = reader.IsDBNull(0) ? "Camera" : $"Camera {reader.GetInt32(0)}",
                    Location = reader.IsDBNull(1) ? "Location unavailable" : reader.GetString(1),
                    Status = status
                };

                model.CameraStatuses.Add(camera);
                if (IsActiveStatus(status))
                {
                    model.ActiveCameras++;
                }
            }

            model.TotalCameras = model.CameraStatuses.Count;
            model.OfflineCameras = Math.Max(0, model.TotalCameras - model.ActiveCameras);
        }

        private static void LoadTodaySummary(OracleConnection connection, DashboardViewModel model)
        {
            const string query = @"
                SELECT
                    COUNT(*) AS TOTAL_EVENTS,
                    NVL(SUM(CASE WHEN UPPER(NVL(ALERT_TYPE, 'UNKNOWN')) = 'AI_DETECTION' THEN 1 ELSE 0 END), 0) AS DETECTIONS,
                    NVL(SUM(CASE WHEN UPPER(NVL(ALERT_TYPE, 'UNKNOWN')) <> 'AI_DETECTION' THEN 1 ELSE 0 END), 0) AS ALERTS,
                    NVL(AVG(CASE WHEN UPPER(NVL(ALERT_TYPE, 'UNKNOWN')) = 'AI_DETECTION' THEN CONFIDENCE_SCORE END), 0) AS AVG_CONFIDENCE
                FROM EVENTS
                WHERE TIMESTAMP >= TRUNC(SYSDATE)";

            using OracleCommand command = new(query, connection);
            using OracleDataReader reader = command.ExecuteReader();
            if (reader.Read())
            {
                model.DetectionsToday = Convert.ToInt32(reader.GetDecimal(1));
                model.AlertsToday = Convert.ToInt32(reader.GetDecimal(2));
                model.AverageConfidence = Convert.ToDouble(reader.GetDecimal(3));
            }
        }

        private static void LoadEventTypeCounts(OracleConnection connection, DashboardViewModel model)
        {
            const string query = @"
                SELECT NVL(ALERT_TYPE, 'Uncategorized') AS EVENT_TYPE, COUNT(*) AS EVENT_COUNT
                FROM EVENTS
                WHERE TIMESTAMP >= TRUNC(SYSDATE)
                GROUP BY NVL(ALERT_TYPE, 'Uncategorized')
                ORDER BY EVENT_COUNT DESC, EVENT_TYPE";

            using OracleCommand command = new(query, connection);
            using OracleDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                model.EventTypeCounts.Add(new DashboardEventTypeCount
                {
                    EventType = reader.GetString(0),
                    Count = Convert.ToInt32(reader.GetDecimal(1))
                });
            }
        }

        private static void LoadActivity(
            OracleConnection connection,
            DashboardViewModel model,
            int rangeHours)
        {
            const string query = @"
                SELECT
                    TRUNC(TIMESTAMP, 'HH24') AS HOUR_BUCKET,
                    SUM(CASE WHEN UPPER(NVL(ALERT_TYPE, 'UNKNOWN')) = 'AI_DETECTION' THEN 1 ELSE 0 END) AS DETECTIONS,
                    SUM(CASE WHEN UPPER(NVL(ALERT_TYPE, 'UNKNOWN')) <> 'AI_DETECTION' THEN 1 ELSE 0 END) AS ALERTS,
                    SUM(CASE WHEN CONFIDENCE_SCORE >= 90 THEN 1 ELSE 0 END) AS HIGH_CONFIDENCE
                FROM EVENTS
                WHERE TIMESTAMP >= TRUNC(SYSDATE, 'HH24') - (:rangeHours - 1) / 24
                GROUP BY TRUNC(TIMESTAMP, 'HH24')
                ORDER BY HOUR_BUCKET";

            using OracleCommand databaseClockCommand = new("SELECT SYSDATE FROM DUAL", connection);
            DateTime databaseNow = Convert.ToDateTime(databaseClockCommand.ExecuteScalar());
            Dictionary<DateTime, DashboardActivityPoint> points = CreateEmptyActivityPoints(rangeHours, databaseNow)
                .ToDictionary(point => point.Hour);

            using OracleCommand command = new(query, connection);
            command.Parameters.Add("rangeHours", OracleDbType.Int32).Value = rangeHours;
            using OracleDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                DateTime bucket = reader.GetDateTime(0);
                DateTime key = new(bucket.Year, bucket.Month, bucket.Day, bucket.Hour, 0, 0);
                if (!points.TryGetValue(key, out DashboardActivityPoint? point))
                {
                    continue;
                }

                point.Detections = Convert.ToInt32(reader.GetDecimal(1));
                point.Alerts = Convert.ToInt32(reader.GetDecimal(2));
                point.HighConfidence = Convert.ToInt32(reader.GetDecimal(3));
            }

            model.ActivityPoints = points.Values.OrderBy(point => point.Hour).ToList();
        }

        private static void LoadRecentEvents(OracleConnection connection, DashboardViewModel model)
        {
            const string query = @"
                SELECT EVENT_ID, CAMERA_ID, ALERT_TYPE, OBJECT_CLASS, CONFIDENCE_SCORE, TIMESTAMP
                FROM EVENTS
                ORDER BY TIMESTAMP DESC
                FETCH FIRST 6 ROWS ONLY";

            using OracleCommand command = new(query, connection);
            using OracleDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                double confidence = reader.IsDBNull(4) ? 0 : Convert.ToDouble(reader.GetDecimal(4));
                int cameraId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                model.RecentAlerts.Add(new DashboardAlertViewModel
                {
                    AlertId = reader.GetInt32(0),
                    CameraName = cameraId == 0 ? "Camera unavailable" : $"Camera {cameraId}",
                    ObjectType = reader.IsDBNull(3) ? "Unknown event" : reader.GetString(3),
                    Severity = confidence >= 90 ? "High" : "Medium",
                    Confidence = confidence,
                    DetectedAt = reader.GetDateTime(5)
                });
            }
        }

        private static List<DashboardActivityPoint> CreateEmptyActivityPoints(
            int rangeHours,
            DateTime? currentTime = null)
        {
            DateTime currentHour = currentTime ?? DateTime.Now;
            currentHour = new DateTime(currentHour.Year, currentHour.Month, currentHour.Day, currentHour.Hour, 0, 0);

            return Enumerable.Range(0, rangeHours)
                .Select(offset =>
                {
                    DateTime hour = currentHour.AddHours(offset - rangeHours + 1);
                    return new DashboardActivityPoint
                    {
                        Hour = hour,
                        Label = hour.ToString("HH")
                    };
                })
                .ToList();
        }

        private static bool IsActiveStatus(string status)
        {
            return status.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase) ||
                   status.Equals("ONLINE", StringComparison.OrdinalIgnoreCase);
        }

        private static void ResetDatabaseValues(DashboardViewModel model)
        {
            model.TotalCameras = 0;
            model.ActiveCameras = 0;
            model.OfflineCameras = 0;
            model.AlertsToday = 0;
            model.DetectionsToday = 0;
            model.AverageConfidence = 0;
            model.CameraStatuses.Clear();
            model.EventTypeCounts.Clear();
            model.RecentAlerts.Clear();
            model.ActivityPoints = CreateEmptyActivityPoints(model.RangeHours);
        }
    }
}
