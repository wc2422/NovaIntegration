using Microsoft.AspNetCore.Mvc;
using NovaIntegration.Web.Models;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System;
using System.Linq;
using NovaIntegration.Web.Services;

namespace NovaIntegration.Web.Controllers
{
    public class CamerasController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly FaceRecognitionDemoService _faceRecognitionDemo;
        private readonly CameraFrameSourceService _cameraFrameSource;
        private readonly ILogger<CamerasController> _logger;

        public CamerasController(
            IConfiguration configuration,
            FaceRecognitionDemoService faceRecognitionDemo,
            CameraFrameSourceService cameraFrameSource,
            ILogger<CamerasController> logger)
        {
            _configuration = configuration;
            _faceRecognitionDemo = faceRecognitionDemo;
            _cameraFrameSource = cameraFrameSource;
            _logger = logger;
        }

        public IActionResult Index()
        {
            List<Camera> cameras = new List<Camera>();
            string? connectionString = _configuration.GetConnectionString("OracleConnection");
            bool databaseEnabled = _configuration.GetValue("Database:Enabled", true);

            if (databaseEnabled && !string.IsNullOrWhiteSpace(connectionString))
            {
                try
                {
                    using OracleConnection connection = new(connectionString);
                    connection.Open();
                    const string query = "SELECT CAMERA_ID, LOCATION FROM CAMERAS";

                    using OracleCommand command = new(query, connection);
                    using OracleDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        int camId = reader.GetInt32(0);
                        string location = reader.GetString(1);

                        cameras.Add(new Camera
                        {
                            CameraId = camId,
                            CameraName = "Camera " + camId,
                            Location = location,
                            IpAddress = $"192.168.1.{20 + camId}",
                            Status = "Online",
                            LastActivity = DateTime.Now,
                            StreamType = "RTSP"
                        });
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Oracle is unavailable. Loading the local camera demo instead.");
                }
            }

            if (cameras.Count == 0)
            {
                cameras.Add(new Camera
                {
                    CameraId = 0,
                    CameraName = "Local Webcam",
                    Location = "This workstation",
                    IpAddress = "Camera index 0",
                    Status = "Online",
                    LastActivity = DateTime.Now,
                    StreamType = "OpenCV"
                });
            }

            return View(cameras);
        }

        [HttpGet]
        public IActionResult GetLiveFrame()
        {
            if (_cameraFrameSource.TryGetFrame(out byte[] frame))
            {
                byte[] annotatedFrame = _faceRecognitionDemo.AnnotateFrame(frame);
                return File(annotatedFrame, "image/jpeg");
            }

            return NoContent();
        }

        [HttpGet]
        public IActionResult CameraStatus()
        {
            return Json(_cameraFrameSource.GetStatus());
        }

        [HttpGet]
        public IActionResult FaceStatus()
        {
            return Json(_faceRecognitionDemo.GetStatus());
        }

        [HttpPost]
        public IActionResult EnrollFace([FromBody] FaceEnrollmentRequest? request)
        {
            string name = request?.Name?.Trim() ?? string.Empty;
            if (!_faceRecognitionDemo.EnrollPendingFace(name))
            {
                return BadRequest(new
                {
                    message = "Enter a name between 1 and 50 characters while an unknown face is visible."
                });
            }

            return Ok(new { name });
        }

        [HttpPost]
        public IActionResult DismissFaceEnrollment()
        {
            _faceRecognitionDemo.DismissPendingEnrollment();
            return NoContent();
        }

        public sealed class FaceEnrollmentRequest
        {
            public string Name { get; set; } = string.Empty;
        }
    }
}
