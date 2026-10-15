using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker.SqlServer;

/// <summary>
/// Writes outbox rows straight into SQL Server for the feed tests (#927), optionally inside a transaction the
/// test keeps open, and returns the position SQL Server gave each row. Plain SQL rather than the repository,
/// because the tests need to control exactly when each write commits. Payloads are synthetic.
/// </summary>
internal static class OutboxRows
{
    private const string Columns =
        "[Id],[TenantId],[MessageType],[TargetPath],[AppointmentId],[Payload],[IdempotencyKey],[Status],"
        + "[AttemptCount],[MaxAttempts],[ExtraProperties],[ConcurrencyStamp],[CreationTime],[IsDeleted]";

    private const string Values =
        "@id,@office,@type,N'api/intake/appointments',@appointment,@payload,@key,@status,"
        + "0,100,N'{}',@stamp,@created,@deleted";

    /// <summary>One inserted row: its id, and the position (rowversion as bigint) SQL Server gave it.</summary>
    public sealed record Inserted(Guid Id, long Position);

    public static async Task<Inserted> InsertAsync(
        string connectionString,
        Guid officeId,
        string payload = "{\"data\":{}}",
        IntegrationOutboxStatus status = IntegrationOutboxStatus.Pending,
        Guid? appointmentId = null,
        bool deleted = false,
        DateTime? createdAt = null)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        return await InsertAsync(connection, null, officeId, payload, status, appointmentId, deleted, createdAt);
    }

    /// <summary>Inserts on <paramref name="connection"/>, inside <paramref name="transaction"/> when given.</summary>
    public static async Task<Inserted> InsertAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        Guid officeId,
        string payload = "{\"data\":{}}",
        IntegrationOutboxStatus status = IntegrationOutboxStatus.Pending,
        Guid? appointmentId = null,
        bool deleted = false,
        DateTime? createdAt = null)
    {
        var id = Guid.NewGuid();
        await using var command = new SqlCommand(
            "INSERT INTO [AppIntegrationOutboxItems] (" + Columns + ") "
            + "OUTPUT CAST(inserted.[ChangeVersion] AS bigint) VALUES (" + Values + ");",
            connection,
            transaction);
        AddParameters(command, id, officeId, payload, status, appointmentId ?? Guid.NewGuid(), deleted, createdAt ?? DateTime.UtcNow);
        var position = (long)(await command.ExecuteScalarAsync())!;
        return new Inserted(id, position);
    }

    /// <summary>Into a database migrated to before #927, where the column does not exist yet.</summary>
    public static async Task InsertWithoutVersionAsync(string connectionString, Guid id)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "INSERT INTO [AppIntegrationOutboxItems] (" + Columns + ") VALUES (" + Values + ");", connection);
        AddParameters(command, id, Guid.NewGuid(), "{\"data\":{}}", IntegrationOutboxStatus.Pending, Guid.NewGuid(), false, DateTime.UtcNow);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Any update to a row, as an alert stamp or a resolve would make; returns the row's NEW position.</summary>
    public static async Task<long> TouchAsync(string connectionString, Guid id)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "UPDATE [AppIntegrationOutboxItems] SET [AlertedAt] = SYSUTCDATETIME() "
            + "OUTPUT CAST(inserted.[ChangeVersion] AS bigint) WHERE [Id] = @id;",
            connection);
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = id });
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static void AddParameters(
        SqlCommand command,
        Guid id,
        Guid officeId,
        string payload,
        IntegrationOutboxStatus status,
        Guid appointmentId,
        bool deleted,
        DateTime createdAt)
    {
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = id });
        command.Parameters.Add(new SqlParameter("@office", SqlDbType.UniqueIdentifier) { Value = officeId });
        command.Parameters.Add(new SqlParameter("@type", SqlDbType.Int) { Value = (int)IntegrationMessageType.Intake });
        command.Parameters.Add(new SqlParameter("@appointment", SqlDbType.UniqueIdentifier) { Value = appointmentId });
        command.Parameters.Add(new SqlParameter("@payload", SqlDbType.NVarChar, -1) { Value = payload });
        command.Parameters.Add(new SqlParameter("@key", SqlDbType.NVarChar, 128) { Value = "key-" + id.ToString("N") });
        command.Parameters.Add(new SqlParameter("@status", SqlDbType.Int) { Value = (int)status });
        command.Parameters.Add(new SqlParameter("@stamp", SqlDbType.NVarChar, 40) { Value = Guid.NewGuid().ToString("N") });
        command.Parameters.Add(new SqlParameter("@created", SqlDbType.DateTime2) { Value = createdAt });
        command.Parameters.Add(new SqlParameter("@deleted", SqlDbType.Bit) { Value = deleted });
    }
}
