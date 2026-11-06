using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.EmailStops;

/// <summary>
/// 2026-11-05 (office decisions 1 + 2) -- THE one place that decides whether an email may go to an address.
/// Staff can stop a person's emails for one case (office table <see cref="AppointmentEmailStop"/>) or for
/// all cases at every practice (host table <see cref="GlobalEmailStop"/>). A stop silences everything the
/// portal sends that person -- case emails, appointment letters, Agree/Decline links, reminders, and CC
/// lines -- except the self-service account mail in <see cref="NeverStoppedTemplateCodes"/>.
///
/// <para>Every sending path asks this service: the notification dispatcher before it writes an outbox row,
/// the outbox drain for rows written before the stop, and the two legacy reminder jobs that enqueue the
/// send job directly. Each lookup costs at most ONE host query and ONE office query for the whole send
/// (or the whole drain batch), never one per recipient.</para>
/// </summary>
public class EmailStopPolicy : IEmailStopPolicy, ITransientDependency
{
    /// <summary>
    /// Self-service account mail a stop never silences: the person asked for it (or staff are onboarding
    /// them), and silencing it would lock them out of their own account. Pinned by a test; the dispatcher
    /// refuses to treat any OTHER template as account mail.
    /// </summary>
    public static readonly IReadOnlyCollection<string> NeverStoppedTemplateCodes = new[]
    {
        NotificationTemplateConsts.Codes.AccountActivation,
        NotificationTemplateConsts.Codes.ResetPassword,
        NotificationTemplateConsts.Codes.PasswordChange,
        NotificationTemplateConsts.Codes.AccountEmailChanged,
        NotificationTemplateConsts.Codes.UserRegistered,
        NotificationTemplateConsts.Codes.InviteExternalUser,
        NotificationTemplateConsts.Codes.ExternalUserPortalLink,
        NotificationTemplateConsts.Codes.InternalUserCreated,
    };

    private readonly IRepository<GlobalEmailStop, Guid> _globalStops;
    private readonly IRepository<AppointmentEmailStop, Guid> _caseStops;
    private readonly ICurrentTenant _currentTenant;
    private readonly IAsyncQueryableExecuter _asyncExecuter;

    public EmailStopPolicy(
        IRepository<GlobalEmailStop, Guid> globalStops,
        IRepository<AppointmentEmailStop, Guid> caseStops,
        ICurrentTenant currentTenant,
        IAsyncQueryableExecuter asyncExecuter)
    {
        _globalStops = globalStops;
        _caseStops = caseStops;
        _currentTenant = currentTenant;
        _asyncExecuter = asyncExecuter;
    }

    public static bool IsNeverStopped(string? templateCode) =>
        !string.IsNullOrWhiteSpace(templateCode)
        && NeverStoppedTemplateCodes.Contains(templateCode, StringComparer.Ordinal);

    /// <summary>
    /// Loads the stops that apply to one send: every address (To and every CC) at once, and the case stops
    /// of <paramref name="appointmentId"/> when the email is about a case. Runs in the current office.
    /// </summary>
    public virtual Task<EmailStopLookup> LookupAsync(IEnumerable<string?> addresses, Guid? appointmentId) =>
        LookupBatchAsync((addresses ?? Array.Empty<string?>()).Select(a => (appointmentId, a)));

    /// <summary>
    /// Loads the stops for many (case, address) pairs at once -- one host query for the all-cases list and
    /// one office query for the case stops, whatever the batch size. Runs in the current office.
    /// </summary>
    public virtual async Task<EmailStopLookup> LookupBatchAsync(
        IEnumerable<(Guid? AppointmentId, string? Address)> pairs)
    {
        var list = (pairs ?? Array.Empty<(Guid?, string?)>()).ToList();
        var normalized = EmailStopAddress.NormalizeAll(list.Select(p => p.Address));
        if (normalized.Count == 0)
        {
            return EmailStopLookup.Empty;
        }

        List<string> allCases;
        using (_currentTenant.Change(null))
        {
            var global = await _globalStops.GetQueryableAsync();
            allCases = await _asyncExecuter.ToListAsync(global
                .Where(x => normalized.Contains(x.NormalizedEmail))
                .Select(x => x.NormalizedEmail));
        }

        var appointmentIds = list
            .Where(p => p.AppointmentId.HasValue)
            .Select(p => p.AppointmentId!.Value)
            .Distinct()
            .ToList();
        var caseStops = new List<(Guid, string)>();
        if (appointmentIds.Count > 0)
        {
            var cases = await _caseStops.GetQueryableAsync();
            var rows = await _asyncExecuter.ToListAsync(cases
                .Where(x => appointmentIds.Contains(x.AppointmentId) && normalized.Contains(x.NormalizedEmail))
                .Select(x => new { x.AppointmentId, x.NormalizedEmail }));
            caseStops = rows
                .Select(x => (x.AppointmentId, x.NormalizedEmail))
                .ToList();
        }

        return new EmailStopLookup(allCases, caseStops);
    }
}
