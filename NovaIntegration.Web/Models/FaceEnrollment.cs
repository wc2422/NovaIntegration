namespace NovaIntegration.Web.Models;

public sealed record FaceEnrollment(
    string FaceId,
    string Name,
    float[] Embedding,
    string ModelVersion,
    DateTime CreatedAtUtc)
{
    public const string CurrentModelVersion = "sface-2021dec-v1";
    public const int EmbeddingDimensions = 128;

    public static FaceEnrollment Create(string faceId, string name, float[] embedding)
    {
        FaceEnrollment enrollment = new(
            faceId,
            name.Trim(),
            (float[])embedding.Clone(),
            CurrentModelVersion,
            DateTime.UtcNow);
        enrollment.Validate();
        return enrollment;
    }

    public void Validate()
    {
        if (!Guid.TryParseExact(FaceId, "N", out _) ||
            string.IsNullOrWhiteSpace(Name) || Name.Length > 50)
        {
            throw new ArgumentException("A face requires an ID and a name of 1 to 50 characters.");
        }

        if (ModelVersion != CurrentModelVersion || Embedding is null || Embedding.Length != EmbeddingDimensions ||
            Embedding.Any(value => !float.IsFinite(value)))
        {
            throw new ArgumentException("The face embedding does not match the current SFace model.");
        }

        double squaredLength = Embedding.Sum(value => (double)value * value);
        if (Math.Abs(squaredLength - 1.0) > 0.01)
        {
            throw new ArgumentException("The face embedding must be normalized before storage.");
        }

        if (CreatedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The face enrollment timestamp must be UTC.");
        }
    }
}
