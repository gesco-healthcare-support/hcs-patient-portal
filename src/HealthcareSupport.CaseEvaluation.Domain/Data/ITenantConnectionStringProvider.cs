namespace HealthcareSupport.CaseEvaluation.Data;

/// <summary>
/// Builds the per-office ("tenant") connection string, at office creation and on every
/// office request (<see cref="OfficeConnectionStringResolver"/>); it is never stored.
/// This is the cloud-agnostic seam: the dev/default implementation
/// derives the connection string from the host "Default" (or an optional
/// App:TenantDbTemplate override), while a production implementation can instead
/// resolve a managed-store secret reference -- without the provisioning code
/// changing. The composed value addresses the office database
/// "CaseEvaluation_{slug}" (see <see cref="MultiTenancy.TenantNaming"/>).
/// Implementations must never log the connection string.
/// </summary>
public interface ITenantConnectionStringProvider
{
    /// <summary>
    /// Builds the connection string for the office identified by
    /// <paramref name="slug"/>.
    /// </summary>
    string BuildConnectionString(string slug);
}
