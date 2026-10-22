using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using NovaIntegration.Web.Models;
using NovaIntegration.Web.Services;
using NovaIntegration.Web.Services.FaceStorage;

await SaveAndLoad();
await FailedSave();
await RetryWithoutDuplicates();
Console.WriteLine("3/3 storage checks passed.");

static async Task SaveAndLoad()
{
    TestRepository repository = new();
    using (FaceRecognitionDemoService service = CreateService(repository))
    {
        await service.LoadEnrollmentsAsync(default);
        string token = StageFace(service);
        Check(await service.EnrollPendingFaceAsync("  Person  ", token, default) == FaceEnrollmentResult.Enrolled,
            "Enrollment failed.");
        FaceEnrollment saved = (await repository.Stored.LoadAsync(default)).Single();
        Check(saved.Name == "Person" && saved.FaceId == token, "Saved data was incorrect.");
    }
    using FaceRecognitionDemoService restarted = CreateService(repository);
    Check(await restarted.LoadEnrollmentsAsync(default) && restarted.GetStatus().KnownFaceCount == 1,
        "A fresh service did not load the saved face.");
    Console.WriteLine("PASS Save and load");
}

static async Task FailedSave()
{
    TestRepository repository = new() { FailSave = true };
    using FaceRecognitionDemoService service = CreateService(repository);
    await service.LoadEnrollmentsAsync(default);
    string token = StageFace(service);
    Check(await service.EnrollPendingFaceAsync("Person", token, default) == FaceEnrollmentResult.StorageUnavailable,
        "Failed save reported success.");
    Check(service.GetStatus().KnownFaceCount == 0 && service.GetStatus().PendingToken == token,
        "Failed save changed the cache or cleared the pending face.");
    Check((await repository.Stored.LoadAsync(default)).Count == 0, "Failed save changed storage.");
    Console.WriteLine("PASS Failed save preserves the pending face");
}

static async Task RetryWithoutDuplicates()
{
    TestRepository repository = new() { FailAfterWrite = true };
    using FaceRecognitionDemoService service = CreateService(repository);
    await service.LoadEnrollmentsAsync(default);
    string token = StageFace(service);
    Check(await service.EnrollPendingFaceAsync("Person", token, default) == FaceEnrollmentResult.StorageUnavailable,
        "Unconfirmed write reported success.");
    repository.FailAfterWrite = false;
    await service.LoadEnrollmentsAsync(default);
    Check(await service.EnrollPendingFaceAsync("Person", token, default) == FaceEnrollmentResult.Enrolled,
        "Retry failed.");
    Check((await repository.Stored.LoadAsync(default)).Count == 1 && service.GetStatus().KnownFaceCount == 1,
        "Retry duplicated the stored face or cache entry.");
    Console.WriteLine("PASS Retry without duplicates");
}

static FaceRecognitionDemoService CreateService(IFaceEnrollmentRepository repository) =>
    new(new TestEnvironment(), NullLogger<FaceRecognitionDemoService>.Instance, repository);

static string StageFace(FaceRecognitionDemoService service)
{
    // Supply synthetic embeddings without a webcam or real biometric data.
    float[] embedding = new float[FaceEnrollment.EmbeddingDimensions];
    embedding[0] = 1;
    MethodInfo track = typeof(FaceRecognitionDemoService).GetMethod("TrackEnrollmentCandidate",
        BindingFlags.NonPublic | BindingFlags.Instance)!;
    for (int frame = 0; frame < 3; frame++)
        track.Invoke(service, new object[] { new[] { embedding } });
    return service.GetStatus().PendingToken ?? throw new Exception("No pending face was staged.");
}

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

sealed class TestRepository : IFaceEnrollmentRepository
{
    public string Provider => "Test";
    public InMemoryFaceEnrollmentRepository Stored { get; } = new();
    public bool FailSave { get; set; }
    public bool FailAfterWrite { get; set; }

    public Task<IReadOnlyList<FaceEnrollment>> LoadAsync(CancellationToken cancellationToken) =>
        Stored.LoadAsync(cancellationToken);

    public async Task SaveAsync(FaceEnrollment enrollment, CancellationToken cancellationToken)
    {
        if (FailSave) throw new IOException("Simulated storage failure.");
        await Stored.SaveAsync(enrollment, cancellationToken);
        if (FailAfterWrite) throw new IOException("Simulated lost database acknowledgement.");
    }
}

sealed class TestEnvironment : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "FaceStorageChecks";
    public string EnvironmentName { get; set; } = "Test";
    public string ContentRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "FaceStorageChecks-NoModels");
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; } = string.Empty;
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}
