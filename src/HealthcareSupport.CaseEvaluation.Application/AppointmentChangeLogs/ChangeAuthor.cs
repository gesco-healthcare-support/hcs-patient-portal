using HealthcareSupport.CaseEvaluation.Identity;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeLogs;

/// <summary>Who made a change, as shown beside a change-log row.</summary>
public sealed record ChangeAuthor(string Name, string Kind)
{
    public static readonly ChangeAuthor System = new("System", ChangeAuthorKinds.System);
    public static readonly ChangeAuthor CaseTracker = new("Case Tracker", ChangeAuthorKinds.CaseTracker);
    public static readonly ChangeAuthor Party = new("Party (email link)", ChangeAuthorKinds.Party);
}

/// <summary>What the audit log (one HTTP request) recorded about its caller.</summary>
public sealed record ChangeAuthorFacts(
    Guid? UserId,
    string? UserName,
    Guid? ImpersonatorUserId,
    string? ImpersonatorUserName,
    string? Url,
    string? ClientId);

/// <summary>An office-scope user, reduced to what the author rules need.</summary>
public sealed record ChangeAuthorUser(string DisplayName, bool IsStaff, Guid? HostOperatorId);

/// <summary>Reads the audit log rows behind a set of changes. One batched query per call.</summary>
public interface IChangeAuthorSource
{
    Task<IReadOnlyDictionary<Guid, ChangeAuthorFacts>> LoadAsync(IReadOnlyCollection<Guid> auditLogIds);
}

/// <summary>User lookups for the author rules (office scope, and host scope for operators).</summary>
public interface IChangeAuthorDirectory
{
    /// <summary>The office user, or null when the account no longer exists.</summary>
    Task<ChangeAuthorUser?> FindOfficeUserAsync(Guid userId);

    /// <summary>The host operator's display name, or null when the operator is gone.</summary>
    Task<string?> FindHostOperatorNameAsync(Guid operatorId);
}

/// <summary>Resolves audit log ids to the person (or system) behind them.</summary>
public interface IChangeAuthorResolver
{
    /// <summary>An audit log that cannot be found resolves to <see cref="ChangeAuthor.System"/>.</summary>
    Task<IReadOnlyDictionary<Guid, ChangeAuthor>> ResolveAsync(IReadOnlyCollection<Guid> auditLogIds);
}

/// <summary>Role names that mark a login as Gesco staff (as opposed to an outside party).</summary>
public static class ChangeAuthorRoles
{
    public static readonly IReadOnlyCollection<string> Staff = new[]
    {
        "admin",
        "IT Admin",
        "Staff Supervisor",
        "Intake Staff",
    };

    public static bool IsStaff(IEnumerable<string> roleNames)
        => roleNames.Any(r => Staff.Contains(r, StringComparer.OrdinalIgnoreCase));
}
