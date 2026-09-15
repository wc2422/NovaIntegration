using OpenCvSharp;

namespace NovaIntegration.Web.Services;

/// <summary>
/// Supplies frames to the existing web endpoint. A fresh shared-buffer frame
/// remains the preferred source; a directly attached webcam is the fallback.
/// </summary>
public sealed class CameraFrameSourceService : IDisposable
{
    private readonly object _sync = new();
    private readonly ILogger<CameraFrameSourceService> _logger;
    private readonly string _configuredSource;
    private readonly string _sharedFramePath;
    private readonly TimeSpan _sharedFrameMaxAge;
    private readonly int _cameraIndex;
    private readonly int _width;
    private readonly int _height;
    private readonly int _framesPerSecond;

    private VideoCapture? _capture;
    private DateTime _nextOpenAttemptUtc;
    private string _activeSource = "None";
    private string? _lastError;
    private bool _disposed;

    public CameraFrameSourceService(
        IConfiguration configuration,
        ILogger<CameraFrameSourceService> logger)
    {
        _logger = logger;
        _configuredSource = configuration["Camera:Source"]?.Trim() ?? "Auto";
        _sharedFramePath = configuration["Camera:SharedFramePath"]?.Trim()
            ?? @"C:\Capstone\SharedBuffer\latest_frame.jpg";
        _sharedFrameMaxAge = TimeSpan.FromMilliseconds(
            Math.Max(250, configuration.GetValue("Camera:SharedFrameMaxAgeMilliseconds", 2000)));
        _cameraIndex = Math.Max(0, configuration.GetValue("Camera:LocalCameraIndex", 0));
        _width = Math.Max(320, configuration.GetValue("Camera:Width", 640));
        _height = Math.Max(240, configuration.GetValue("Camera:Height", 480));
        _framesPerSecond = Math.Clamp(configuration.GetValue("Camera:FramesPerSecond", 15), 1, 60);
    }

    public bool TryGetFrame(out byte[] jpegBytes)
    {
        lock (_sync)
        {
            jpegBytes = [];
            if (_disposed)
            {
                return false;
            }

            bool allowSharedBuffer = !string.Equals(
                _configuredSource,
                "LocalCamera",
                StringComparison.OrdinalIgnoreCase);
            bool allowLocalCamera = !string.Equals(
                _configuredSource,
                "SharedBuffer",
                StringComparison.OrdinalIgnoreCase);
            bool requireFreshSharedFrame = allowLocalCamera;

            if (allowSharedBuffer &&
                TryReadSharedFrame(requireFreshSharedFrame, out jpegBytes))
            {
                ReleaseCapture();
                _activeSource = "SharedBuffer";
                _lastError = null;
                return true;
            }

            if (allowLocalCamera && TryReadLocalCamera(out jpegBytes))
            {
                _activeSource = $"LocalCamera:{_cameraIndex}";
                _lastError = null;
                return true;
            }

            _activeSource = "None";
            return false;
        }
    }

    public CameraFrameSourceStatus GetStatus()
    {
        lock (_sync)
        {
            return new CameraFrameSourceStatus(
                _configuredSource,
                _activeSource,
                _cameraIndex,
                _sharedFramePath,
                _lastError);
        }
    }

    private bool TryReadSharedFrame(bool requireFreshFrame, out byte[] jpegBytes)
    {
        jpegBytes = [];

        try
        {
            FileInfo frameFile = new(_sharedFramePath);
            if (!frameFile.Exists ||
                (requireFreshFrame &&
                 DateTime.UtcNow - frameFile.LastWriteTimeUtc > _sharedFrameMaxAge))
            {
                return false;
            }

            using FileStream fileStream = new(
                _sharedFramePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);
            using MemoryStream memoryStream = new();
            fileStream.CopyTo(memoryStream);
            jpegBytes = memoryStream.ToArray();
            return jpegBytes.Length > 0;
        }
        catch (Exception exception)
        {
            SetError($"Shared frame could not be read: {exception.Message}");
            return false;
        }
    }

    private bool TryReadLocalCamera(out byte[] jpegBytes)
    {
        jpegBytes = [];

        if (!EnsureCaptureOpen())
        {
            return false;
        }

        try
        {
            using Mat frame = new();
            if (!_capture!.Read(frame) || frame.Empty())
            {
                SetError($"Camera {_cameraIndex} returned an empty frame.");
                ReleaseCapture();
                _nextOpenAttemptUtc = DateTime.UtcNow.AddSeconds(2);
                return false;
            }

            Cv2.ImEncode(
                ".jpg",
                frame,
                out jpegBytes,
                new ImageEncodingParam(ImwriteFlags.JpegQuality, 90));
            return jpegBytes.Length > 0;
        }
        catch (Exception exception)
        {
            SetError($"Camera {_cameraIndex} capture failed: {exception.Message}");
            ReleaseCapture();
            _nextOpenAttemptUtc = DateTime.UtcNow.AddSeconds(2);
            return false;
        }
    }

    private bool EnsureCaptureOpen()
    {
        if (_capture?.IsOpened() == true)
        {
            return true;
        }

        if (DateTime.UtcNow < _nextOpenAttemptUtc)
        {
            return false;
        }

        ReleaseCapture();

        foreach (VideoCaptureAPIs backend in new[]
                 {
                     VideoCaptureAPIs.DSHOW,
                     VideoCaptureAPIs.MSMF,
                     VideoCaptureAPIs.ANY
                 })
        {
            VideoCapture candidate = new();
            try
            {
                if (!candidate.Open(_cameraIndex, backend) || !candidate.IsOpened())
                {
                    candidate.Dispose();
                    continue;
                }

                candidate.Set(VideoCaptureProperties.FrameWidth, _width);
                candidate.Set(VideoCaptureProperties.FrameHeight, _height);
                candidate.Set(VideoCaptureProperties.Fps, _framesPerSecond);
                candidate.Set(VideoCaptureProperties.BufferSize, 1);
                _capture = candidate;
                _logger.LogInformation(
                    "Opened local camera {CameraIndex} with {Backend} at requested {Width}x{Height}.",
                    _cameraIndex,
                    backend,
                    _width,
                    _height);
                return true;
            }
            catch (Exception exception)
            {
                candidate.Dispose();
                SetError(
                    $"Camera {_cameraIndex} could not open with {backend}: {exception.Message}");
            }
        }

        SetError(
            $"Camera {_cameraIndex} could not be opened. Check that it is connected and not in use by another app.");
        _nextOpenAttemptUtc = DateTime.UtcNow.AddSeconds(3);
        return false;
    }

    private void SetError(string message)
    {
        if (!string.Equals(_lastError, message, StringComparison.Ordinal))
        {
            _logger.LogWarning("{CameraError}", message);
        }

        _lastError = message;
    }

    private void ReleaseCapture()
    {
        _capture?.Release();
        _capture?.Dispose();
        _capture = null;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            ReleaseCapture();
            _disposed = true;
        }
    }
}

public sealed record CameraFrameSourceStatus(
    string ConfiguredSource,
    string ActiveSource,
    int LocalCameraIndex,
    string SharedFramePath,
    string? Error);
