using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeLogs;

/// <summary>
/// The "who" rules for the change log. Pure over two seams (<see cref="IChangeAuthorSource"/> and
/// <see cref="IChangeAuthorDirectory"/>) so every branch is unit-tested without a database.
///
/// <para>Order matters. A host operator acts in an office through a SHADOW login, so the request's
/// audit row names the shadow as the user and carries the real operator as the impersonator. The
/// impersonator therefore wins over everything else. A shadow that signed in directly (no
/// impersonator on the row) is still recognised by its stamp and mapped to the operator. Callers with
/// no user at all are classed by the URL they hit: the Case Tracker integration, or the anonymous
/// email-link pages; anything else (background work) is "System".</para>
/// </summary>
public class ChangeAuthorResolver : IChangeAuthorResolver, ITransientDependency
{
    private const string CaseTrackerUrlPrefix = "/api/integration/";

    // The anonymous endpoints a party reaches from an emailed link.
    private static readonly string[] PartyLinkUrlPrefixes =
    {
        "/api/public/change-request-consent",
        "/api/public/appointment-documents",
    };

    private readonly IChangeAuthorSource _source;
    private readonly IChangeAuthorDirectory _directory;

    public ChangeAuthorResolver(IChangeAuthorSource source, IChangeAuthorDirectory directory)
    {
        _source = source;
        _directory = directory;
    }

    public virtual async Task<IReadOnlyDictionary<Guid, ChangeAuthor>> ResolveAsync(
        IReadOnlyCollection<Guid> auditLogIds)
    {
        var result = new Dictionary<Guid, ChangeAuthor>();
        var ids = auditLogIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return result;
        }

        var facts = await _source.LoadAsync(ids);
        // One lookup per distinct person, however many audit logs they appear on.
        var hostNames = new Dictionary<Guid, string?>();
        var officeUsers = new Dictionary<Guid, ChangeAuthorUser?>();

        foreach (var id in ids)
        {
            result[id] = facts.TryGetValue(id, out var f)
                ? await DecideAsync(f, hostNames, officeUsers)
                : ChangeAuthor.System;
        }
        return result;
    }

    private async Task<ChangeAuthor> DecideAsync(
        ChangeAuthorFacts facts,
        Dictionary<Guid, string?> hostNames,
        Dictionary<Guid, ChangeAuthorUser?> officeUsers)
    {
        if (facts.ImpersonatorUserId is { } operatorId)
        {
            var name = await HostNameAsync(operatorId, hostNames);
            return new ChangeAuthor(
                FirstNonBlank(name, facts.ImpersonatorUserName) ?? "Host staff",
                ChangeAuthorKinds.HostStaff);
        }

        if (facts.UserId is { } userId)
        {
            if (!officeUsers.TryGetValue(userId, out var user))
            {
                user = await _directory.FindOfficeUserAsync(userId);
                officeUsers[userId] = user;
            }

            if (user == null)
            {
                // The account was deleted since; the audit row still remembers the sign-in name.
                return new ChangeAuthor(FirstNonBlank(facts.UserName) ?? "Unknown user", ChangeAuthorKinds.Outside);
            }

            if (user.HostOperatorId is { } stampedOperator)
            {
                var name = await HostNameAsync(stampedOperator, hostNames);
                return new ChangeAuthor(FirstNonBlank(name, user.DisplayName) ?? "Host staff", ChangeAuthorKinds.HostStaff);
            }

            return new ChangeAuthor(
                FirstNonBlank(user.DisplayName, facts.UserName) ?? "Unknown user",
                user.IsStaff ? ChangeAuthorKinds.Staff : ChangeAuthorKinds.Outside);
        }

        var url = facts.Url ?? string.Empty;
        if (url.StartsWith(CaseTrackerUrlPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return ChangeAuthor.CaseTracker;
        }
        if (PartyLinkUrlPrefixes.Any(p => url.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            return ChangeAuthor.Party;
        }
        return ChangeAuthor.System;
    }

    private async Task<string?> HostNameAsync(Guid operatorId, Dictionary<Guid, string?> cache)
    {
        if (!cache.TryGetValue(operatorId, out var name))
        {
            name = await _directory.FindHostOperatorNameAsync(operatorId);
            cache[operatorId] = name;
        }
        return name;
    }

    private static string? FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}
