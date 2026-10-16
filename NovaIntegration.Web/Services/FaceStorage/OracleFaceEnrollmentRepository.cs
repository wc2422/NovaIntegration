using System.Text.Json;
using NovaIntegration.Web.Models;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace NovaIntegration.Web.Services.FaceStorage;

public sealed class OracleFaceEnrollmentRepository : IFaceEnrollmentRepository
{
    private readonly string _connectionString;
    private readonly ILogger<OracleFaceEnrollmentRepository> _logger;
    public string Provider => "Oracle";

    public OracleFaceEnrollmentRepository(
        IConfiguration configuration,
        ILogger<OracleFaceEnrollmentRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("OracleConnection") ?? string.Empty;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FaceEnrollment>> LoadAsync(CancellationToken cancellationToken)
    {
        using OracleConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using OracleCommand command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandTimeout = 15;
        command.CommandText = """
            SELECT FACE_ID, DISPLAY_NAME, EMBEDDING_JSON, CREATED_AT_UTC
            FROM FACE_ENROLLMENTS
            WHERE MODEL_VERSION = :model_version AND EMBEDDING_DIMENSIONS = :dimensions
            ORDER BY CREATED_AT_UTC, FACE_ID
            """;
        command.Parameters.Add("model_version", OracleDbType.Varchar2, 64).Value =
            FaceEnrollment.CurrentModelVersion;
        command.Parameters.Add("dimensions", OracleDbType.Int32).Value =
            FaceEnrollment.EmbeddingDimensions;

        using OracleDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        List<FaceEnrollment> faces = [];
        while (await reader.ReadAsync(cancellationToken))
        {
            try
            {
                if (Enumerable.Range(0, 4).Any(reader.IsDBNull))
                {
                    throw new ArgumentException("Enrollment data contains null fields.");
                }
                using OracleClob embeddingJson = reader.GetOracleClob(2);
                float[] embedding = JsonSerializer.Deserialize<float[]>(embeddingJson.Value)
                    ?? throw new ArgumentException("Embedding JSON was null.");
                FaceEnrollment enrollment = new(
                    reader.GetString(0),
                    reader.GetString(1),
                    embedding,
                    FaceEnrollment.CurrentModelVersion,
                    DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc));
                enrollment.Validate();
                faces.Add(enrollment);
            }
            catch (Exception exception) when (exception is JsonException or ArgumentException)
            {
                // Do not log the biometric template or person's name.
                _logger.LogWarning("Skipped a face enrollment with invalid template data.");
            }
        }
        return faces;
    }

    public async Task SaveAsync(FaceEnrollment enrollment, CancellationToken cancellationToken)
    {
        enrollment.Validate();
        using OracleConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using OracleCommand command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandTimeout = 15;
        command.CommandText = """
            MERGE INTO FACE_ENROLLMENTS target
            USING (SELECT :face_id AS FACE_ID FROM DUAL) source
            ON (target.FACE_ID = source.FACE_ID)
            WHEN MATCHED THEN UPDATE SET
                target.DISPLAY_NAME = :display_name,
                target.EMBEDDING_JSON = :embedding_json,
                target.MODEL_VERSION = :model_version,
                target.EMBEDDING_DIMENSIONS = :dimensions,
                target.CREATED_AT_UTC = :created_at_utc
            WHEN NOT MATCHED THEN INSERT
                (FACE_ID, DISPLAY_NAME, EMBEDDING_JSON, MODEL_VERSION,
                 EMBEDDING_DIMENSIONS, CREATED_AT_UTC)
            VALUES (source.FACE_ID, :display_name, :embedding_json, :model_version,
                    :dimensions, :created_at_utc)
            """;
        command.Parameters.Add("face_id", OracleDbType.Varchar2, 32).Value = enrollment.FaceId;
        command.Parameters.Add("display_name", OracleDbType.Varchar2, 50).Value = enrollment.Name;
        command.Parameters.Add("embedding_json", OracleDbType.Clob).Value =
            JsonSerializer.Serialize(enrollment.Embedding);
        command.Parameters.Add("model_version", OracleDbType.Varchar2, 64).Value = enrollment.ModelVersion;
        command.Parameters.Add("dimensions", OracleDbType.Int32).Value = enrollment.Embedding.Length;
        command.Parameters.Add("created_at_utc", OracleDbType.TimeStamp).Value = enrollment.CreatedAtUtc;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
