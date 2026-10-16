using NovaIntegration.Web.Models;

namespace NovaIntegration.Web.Services.FaceStorage;

public interface IFaceEnrollmentRepository
{
    string Provider { get; }
    Task<IReadOnlyList<FaceEnrollment>> LoadAsync(CancellationToken cancellationToken);
    // Saving the same FaceId again must succeed without creating a second enrollment.
    Task SaveAsync(FaceEnrollment enrollment, CancellationToken cancellationToken);
}
