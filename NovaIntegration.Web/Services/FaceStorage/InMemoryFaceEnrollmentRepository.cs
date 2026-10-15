using NovaIntegration.Web.Models;

namespace NovaIntegration.Web.Services.FaceStorage;

public sealed class InMemoryFaceEnrollmentRepository : IFaceEnrollmentRepository
{
    private readonly object _sync = new();
    private readonly List<FaceEnrollment> _faces = [];
    public string Provider => "Memory";

    public Task<IReadOnlyList<FaceEnrollment>> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            IReadOnlyList<FaceEnrollment> faces = _faces
                .Select(face => face with { Embedding = (float[])face.Embedding.Clone() })
                .ToArray();
            return Task.FromResult(faces);
        }
    }

    public Task SaveAsync(FaceEnrollment enrollment, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        enrollment.Validate();
        lock (_sync)
        {
            FaceEnrollment copy = enrollment with { Embedding = (float[])enrollment.Embedding.Clone() };
            int index = _faces.FindIndex(face => face.FaceId == enrollment.FaceId);
            if (index >= 0)
            {
                _faces[index] = copy;
            }
            else
            {
                _faces.Add(copy);
            }
        }
        return Task.CompletedTask;
    }
}
