using Microsoft.AspNetCore.Mvc;
using NovaIntegration.Web.Models;

namespace NovaIntegration.Web.Controllers
{
    public class CamerasController : Controller
    {
        public IActionResult Index()
        {
            List<Camera> cameras = new List<Camera>
            {
                new Camera
                {
                    CameraId = 1,
                    CameraName = "Main Entrance Camera",
                    Location = "Building Entrance",
                    IpAddress = "192.168.1.21",
                    Status = "Online",
                    LastActivity = DateTime.Now.AddMinutes(-1),
                    StreamType = "RTSP"
                },
                new Camera
                {
                    CameraId = 2,
                    CameraName = "West Parking Lot",
                    Location = "Parking Area",
                    IpAddress = "192.168.1.45",
                    Status = "Online",
                    LastActivity = DateTime.Now.AddMinutes(-3),
                    StreamType = "RTSP"
                },
                new Camera
                {
                    CameraId = 3,
                    CameraName = "Loading Area Camera",
                    Location = "Rear Entrance",
                    IpAddress = "192.168.1.62",
                    Status = "Offline",
                    LastActivity = DateTime.Now.AddHours(-2),
                    StreamType = "RTSP"
                },
                new Camera
                {
                    CameraId = 4,
                    CameraName = "Floor Two Hallway",
                    Location = "Second Floor",
                    IpAddress = "192.168.1.74",
                    Status = "Online",
                    LastActivity = DateTime.Now.AddMinutes(-5),
                    StreamType = "ONVIF"
                },
                new Camera
                {
                    CameraId = 5,
                    CameraName = "Emergency Exit Camera",
                    Location = "East Exit",
                    IpAddress = "192.168.1.88",
                    Status = "Maintenance",
                    LastActivity = DateTime.Now.AddHours(-5),
                    StreamType = "RTSP"
                },
                new Camera
                {
                    CameraId = 6,
                    CameraName = "Reception Camera",
                    Location = "Main Lobby",
                    IpAddress = "192.168.1.95",
                    Status = "Online",
                    LastActivity = DateTime.Now.AddMinutes(-2),
                    StreamType = "ONVIF"
                }
            };

            return View(cameras);
        }
    }
}