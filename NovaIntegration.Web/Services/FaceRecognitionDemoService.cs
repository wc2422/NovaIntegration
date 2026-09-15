using OpenCvSharp;
using OpenCvSharp.Dnn;

namespace NovaIntegration.Web.Services;

/// <summary>
/// Lightweight proof-of-concept face recognition. All enrolled faces live only
/// in this singleton's memory and are discarded when the web app stops.
/// </summary>
public sealed class FaceRecognitionDemoService : IDisposable
{
    private const float DetectionThreshold = 0.85f;
    private const float RecognitionThreshold = 0.363f;
    private const float CandidateSimilarityThreshold = 0.75f;
    private const int StableFramesRequired = 3;
    private const int MaxDetectionDimension = 640;

    private static readonly Point2f[] SFaceLandmarks =
    [
        new(38.2946f, 51.6963f),
        new(73.5318f, 51.5014f),
        new(56.0252f, 71.7366f),
        new(41.5493f, 92.3655f),
        new(70.7299f, 92.2041f)
    ];

    private readonly object _sync = new();
    private readonly ILogger<FaceRecognitionDemoService> _logger;
    private readonly string _detectorModelPath;
    private readonly List<KnownFace> _knownFaces = [];

    private FaceDetectorYN? _detector;
    private Net? _recognizer;
    private Size _detectorInputSize;
    private float[]? _candidateEmbedding;
    private int _candidateFrames;
    private float[]? _pendingEmbedding;
    private string? _pendingToken;
    private DateTime _enrollmentCooldownUntilUtc;
    private string? _lastError;
    private bool _disposed;

    public FaceRecognitionDemoService(
        IWebHostEnvironment environment,
        ILogger<FaceRecognitionDemoService> logger)
    {
        _logger = logger;
        _detectorModelPath = Path.Combine(
            environment.ContentRootPath,
            "FaceModels",
            "face_detection_yunet_2023mar.onnx");

        string recognizerModelPath = Path.Combine(
            environment.ContentRootPath,
            "FaceModels",
            "face_recognition_sface_2021dec.onnx");

        try
        {
            if (!File.Exists(_detectorModelPath) || !File.Exists(recognizerModelPath))
            {
                throw new FileNotFoundException(
                    "YuNet or SFace model file is missing from the FaceModels folder.");
            }

            _recognizer = CvDnn.ReadNetFromOnnx(recognizerModelPath);
            _logger.LogInformation(
                "In-memory facial recognition demo initialized with YuNet and SFace.");
        }
        catch (Exception exception)
        {
            _lastError = exception.Message;
            _logger.LogWarning(
                exception,
                "Facial recognition is unavailable; camera frames will remain unchanged.");
        }
    }

    public byte[] AnnotateFrame(byte[] jpegBytes)
    {
        if (jpegBytes.Length == 0)
        {
            return jpegBytes;
        }

        lock (_sync)
        {
            if (_recognizer is null || _disposed)
            {
                return jpegBytes;
            }

            try
            {
                using Mat frame = Cv2.ImDecode(jpegBytes, ImreadModes.Color);
                if (frame.Empty())
                {
                    return jpegBytes;
                }

                using Mat reducedFrame = new();
                Mat detectorInput = frame;
                float detectorScale = Math.Min(
                    1.0f,
                    MaxDetectionDimension / (float)Math.Max(frame.Width, frame.Height));

                if (detectorScale < 1.0f)
                {
                    Cv2.Resize(
                        frame,
                        reducedFrame,
                        new Size(
                            Math.Max(1, (int)MathF.Round(frame.Width * detectorScale)),
                            Math.Max(1, (int)MathF.Round(frame.Height * detectorScale))));
                    detectorInput = reducedFrame;
                }

                EnsureDetector(detectorInput.Size());
                using Mat faces = new();
                _detector!.Detect(detectorInput, faces);

                List<float[]> unknownEmbeddings = [];

                for (int row = 0; row < faces.Rows; row++)
                {
                    float coordinateScale = 1.0f / detectorScale;
                    Rect faceBox = GetClippedFaceBox(
                        faces,
                        row,
                        frame.Size(),
                        coordinateScale);
                    if (faceBox.Width <= 0 || faceBox.Height <= 0)
                    {
                        continue;
                    }

                    float[] embedding = ExtractEmbedding(
                        frame,
                        faces,
                        row,
                        coordinateScale);
                    KnownFace? knownFace = FindBestMatch(embedding, out float similarity);
                    bool isKnown = knownFace is not null && similarity >= RecognitionThreshold;

                    if (!isKnown)
                    {
                        unknownEmbeddings.Add(embedding);
                    }

                    DrawLabel(
                        frame,
                        faceBox,
                        isKnown ? knownFace!.Name : "Unknown",
                        isKnown);
                }

                TrackEnrollmentCandidate(unknownEmbeddings);

                Cv2.ImEncode(
                    ".jpg",
                    frame,
                    out byte[] annotatedBytes,
                    new ImageEncodingParam(ImwriteFlags.JpegQuality, 90));

                _lastError = null;
                return annotatedBytes;
            }
            catch (Exception exception)
            {
                if (!string.Equals(_lastError, exception.Message, StringComparison.Ordinal))
                {
                    _logger.LogWarning(
                        exception,
                        "A face-recognition frame failed; returning the original camera frame.");
                }

                _lastError = exception.Message;
                return jpegBytes;
            }
        }
    }

    public FaceRecognitionDemoStatus GetStatus()
    {
        lock (_sync)
        {
            return new FaceRecognitionDemoStatus(
                _recognizer is not null && !_disposed,
                _pendingEmbedding is not null,
                _pendingToken,
                _knownFaces.Count,
                _lastError);
        }
    }

    public bool EnrollPendingFace(string? name)
    {
        string normalizedName = (name ?? string.Empty).Trim();
        if (normalizedName.Length is < 1 or > 50)
        {
            return false;
        }

        lock (_sync)
        {
            if (_pendingEmbedding is null)
            {
                return false;
            }

            _knownFaces.Add(new KnownFace(normalizedName, (float[])_pendingEmbedding.Clone()));
            ClearPendingEnrollment();
            _enrollmentCooldownUntilUtc = DateTime.UtcNow.AddSeconds(10);
            _logger.LogInformation(
                "Enrolled {Name} in the in-memory facial recognition demo.",
                normalizedName);
            return true;
        }
    }

    public void DismissPendingEnrollment()
    {
        lock (_sync)
        {
            ClearPendingEnrollment();
            _enrollmentCooldownUntilUtc = DateTime.UtcNow.AddSeconds(5);
        }
    }

    private void EnsureDetector(Size inputSize)
    {
        if (_detector is not null && _detectorInputSize == inputSize)
        {
            return;
        }

        _detector?.Dispose();
        _detector = FaceDetectorYN.Create(
            _detectorModelPath,
            string.Empty,
            inputSize,
            DetectionThreshold,
            0.3f,
            5000,
            (Backend)0,
            (Target)0);
        _detectorInputSize = inputSize;
    }

    private float[] ExtractEmbedding(
        Mat frame,
        Mat faces,
        int row,
        float coordinateScale)
    {
        Point2f[] sourceLandmarks = new Point2f[5];
        for (int index = 0; index < sourceLandmarks.Length; index++)
        {
            sourceLandmarks[index] = new Point2f(
                faces.At<float>(row, 4 + (index * 2)) * coordinateScale,
                faces.At<float>(row, 5 + (index * 2)) * coordinateScale);
        }

        using Mat transform = CalculateSimilarityTransform(sourceLandmarks, SFaceLandmarks);
        using Mat alignedFace = new();
        Cv2.WarpAffine(
            frame,
            alignedFace,
            transform,
            new Size(112, 112),
            InterpolationFlags.Linear,
            BorderTypes.Constant);

        using Mat blob = CvDnn.BlobFromImage(
            alignedFace,
            1.0,
            new Size(112, 112),
            new Scalar(0, 0, 0),
            true,
            false);

        _recognizer!.SetInput(blob, string.Empty);
        using Mat feature = _recognizer.Forward(string.Empty);
        feature.GetArray(out float[] values);
        Normalize(values);
        return values;
    }

    private static Mat CalculateSimilarityTransform(Point2f[] source, Point2f[] destination)
    {
        double sourceMeanX = source.Average(point => point.X);
        double sourceMeanY = source.Average(point => point.Y);
        double destinationMeanX = destination.Average(point => point.X);
        double destinationMeanY = destination.Average(point => point.Y);

        double denominator = 0;
        double scaleRotationA = 0;
        double scaleRotationB = 0;

        for (int index = 0; index < source.Length; index++)
        {
            double sourceX = source[index].X - sourceMeanX;
            double sourceY = source[index].Y - sourceMeanY;
            double destinationX = destination[index].X - destinationMeanX;
            double destinationY = destination[index].Y - destinationMeanY;

            denominator += (sourceX * sourceX) + (sourceY * sourceY);
            scaleRotationA += (sourceX * destinationX) + (sourceY * destinationY);
            scaleRotationB += (sourceX * destinationY) - (sourceY * destinationX);
        }

        if (denominator <= double.Epsilon)
        {
            throw new InvalidOperationException("Face landmarks could not be aligned.");
        }

        double a = scaleRotationA / denominator;
        double b = scaleRotationB / denominator;
        double translationX = destinationMeanX - (a * sourceMeanX) + (b * sourceMeanY);
        double translationY = destinationMeanY - (b * sourceMeanX) - (a * sourceMeanY);

        Mat transform = new(2, 3, MatType.CV_64FC1);
        transform.Set(0, 0, a);
        transform.Set(0, 1, -b);
        transform.Set(0, 2, translationX);
        transform.Set(1, 0, b);
        transform.Set(1, 1, a);
        transform.Set(1, 2, translationY);
        return transform;
    }

    private KnownFace? FindBestMatch(float[] embedding, out float bestSimilarity)
    {
        KnownFace? bestMatch = null;
        bestSimilarity = float.MinValue;

        foreach (KnownFace knownFace in _knownFaces)
        {
            float similarity = CosineSimilarity(embedding, knownFace.Embedding);
            if (similarity > bestSimilarity)
            {
                bestSimilarity = similarity;
                bestMatch = knownFace;
            }
        }

        return bestMatch;
    }

    private void TrackEnrollmentCandidate(IReadOnlyList<float[]> unknownEmbeddings)
    {
        if (_pendingEmbedding is not null ||
            DateTime.UtcNow < _enrollmentCooldownUntilUtc ||
            unknownEmbeddings.Count != 1)
        {
            if (unknownEmbeddings.Count != 1)
            {
                _candidateEmbedding = null;
                _candidateFrames = 0;
            }

            return;
        }

        float[] embedding = unknownEmbeddings[0];
        if (_candidateEmbedding is null ||
            CosineSimilarity(_candidateEmbedding, embedding) < CandidateSimilarityThreshold)
        {
            _candidateEmbedding = (float[])embedding.Clone();
            _candidateFrames = 1;
            return;
        }

        _candidateEmbedding = AverageAndNormalize(
            _candidateEmbedding,
            embedding,
            _candidateFrames);
        _candidateFrames++;

        if (_candidateFrames >= StableFramesRequired)
        {
            _pendingEmbedding = (float[])_candidateEmbedding.Clone();
            _pendingToken = Guid.NewGuid().ToString("N");
            _candidateEmbedding = null;
            _candidateFrames = 0;
        }
    }

    private void ClearPendingEnrollment()
    {
        _pendingEmbedding = null;
        _pendingToken = null;
        _candidateEmbedding = null;
        _candidateFrames = 0;
    }

    private static Rect GetClippedFaceBox(
        Mat faces,
        int row,
        Size frameSize,
        float coordinateScale)
    {
        float rawLeft = faces.At<float>(row, 0) * coordinateScale;
        float rawTop = faces.At<float>(row, 1) * coordinateScale;
        float rawWidth = faces.At<float>(row, 2) * coordinateScale;
        float rawHeight = faces.At<float>(row, 3) * coordinateScale;

        int left = Math.Clamp((int)MathF.Round(rawLeft), 0, frameSize.Width - 1);
        int top = Math.Clamp((int)MathF.Round(rawTop), 0, frameSize.Height - 1);
        int right = Math.Clamp(
            (int)MathF.Round(rawLeft + rawWidth),
            left + 1,
            frameSize.Width);
        int bottom = Math.Clamp(
            (int)MathF.Round(rawTop + rawHeight),
            top + 1,
            frameSize.Height);

        return new Rect(left, top, right - left, bottom - top);
    }

    private static void DrawLabel(Mat frame, Rect faceBox, string label, bool isKnown)
    {
        Scalar color = isKnown
            ? new Scalar(40, 210, 40)
            : new Scalar(0, 165, 255);

        Cv2.Rectangle(frame, faceBox, color, 2);
        Size textSize = Cv2.GetTextSize(
            label,
            HersheyFonts.HersheySimplex,
            0.65,
            2,
            out int baseline);

        int labelTop = Math.Max(0, faceBox.Y - textSize.Height - baseline - 8);
        int labelWidth = Math.Min(frame.Width - faceBox.X, textSize.Width + 12);
        int labelHeight = textSize.Height + baseline + 8;
        Cv2.Rectangle(
            frame,
            new Rect(faceBox.X, labelTop, labelWidth, labelHeight),
            color,
            -1);
        Cv2.PutText(
            frame,
            label,
            new Point(faceBox.X + 6, labelTop + textSize.Height + 2),
            HersheyFonts.HersheySimplex,
            0.65,
            Scalar.White,
            2,
            LineTypes.AntiAlias);
    }

    private static float[] AverageAndNormalize(float[] current, float[] next, int currentCount)
    {
        float[] average = new float[current.Length];
        for (int index = 0; index < average.Length; index++)
        {
            average[index] = ((current[index] * currentCount) + next[index]) / (currentCount + 1);
        }

        Normalize(average);
        return average;
    }

    private static float CosineSimilarity(float[] first, float[] second)
    {
        int length = Math.Min(first.Length, second.Length);
        float dotProduct = 0;
        for (int index = 0; index < length; index++)
        {
            dotProduct += first[index] * second[index];
        }

        return dotProduct;
    }

    private static void Normalize(float[] values)
    {
        double squaredLength = 0;
        foreach (float value in values)
        {
            squaredLength += value * value;
        }

        double length = Math.Sqrt(squaredLength);
        if (length <= double.Epsilon)
        {
            return;
        }

        for (int index = 0; index < values.Length; index++)
        {
            values[index] = (float)(values[index] / length);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _detector?.Dispose();
            _recognizer?.Dispose();
            _disposed = true;
        }
    }

    private sealed record KnownFace(string Name, float[] Embedding);
}

public sealed record FaceRecognitionDemoStatus(
    bool Enabled,
    bool PendingEnrollment,
    string? PendingToken,
    int KnownFaceCount,
    string? Error);
