using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.SystemParameters;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Phase 2.B (Category 2, 2026-05-08): shared "append per-tenant CC recipients"
/// helper. Reads <see cref="SystemParameter.CcEmailIds"/> (semicolon-separated)
/// for the current tenant scope and appends each address as an additional
/// <see cref="NotificationRecipient"/> with role <see cref="RecipientRole.OfficeAdmin"/>.
/// Dedupes against any address already in the recipient list so a CC address
/// that's also a stakeholder doesn't double-send.
///
/// <para>OLD-parity context: OLD's <c>AppointmentDomain.cs</c>:931, 954 reads
/// the global <c>ServerSetting.clinicStaffEmail</c> and passes it as the
/// 4th-arg <c>emailCC</c> on the SendSMTPMail overload. NEW expresses the
/// same intent per-tenant via <c>SystemParameter.CcEmailIds</c>; this helper
/// is the single seam where the wiring lives so the two callers
/// (<c>BookingSubmissionEmailHandler.DispatchApproveRejectToStaffWhenBookerIsExternalAsync</c>
/// and <c>StatusChangeEmailHandler.DispatchApprovedAsync</c>) stay
/// behaviorally consistent.</para>
///
/// <para>NOT applied to Pending stakeholders (Adrian directive 2026-05-08
/// Decision 2.1: NO CC on the AppointmentRequested fan-out -- the office
/// mailbox is already on the recipient list as its own dedicated email)
/// or to Rejected (OLD's 3-arg SendSMTPMail overload at :990 has no CC).</para>
/// </summary>
public class CcRecipientAppender : ITransientDependency
{
    private readonly ISystemParameterRepository _systemParameterRepository;
    private readonly ILogger<CcRecipientAppender> _logger;

    public CcRecipientAppender(
        ISystemParameterRepository systemParameterRepository,
        ILogger<CcRecipientAppender> logger)
    {
        _systemParameterRepository = systemParameterRepository;
        _logger = logger;
    }

    /// <summary>
    /// Appends CC addresses from the current tenant's
    /// <see cref="SystemParameter.CcEmailIds"/> column (semicolon- or
    /// comma-separated) to <paramref name="recipients"/>. Mutates the
    /// list in place. No-ops when the row is missing or the column is
    /// empty -- the dispatcher handles a CC-less recipient list cleanly.
    /// </summary>
    /// <param name="recipients">
    /// The mutable recipient list the dispatcher will receive. CC entries
    /// are appended; stakeholder entries are not modified.
    /// </param>
    /// <param name="contextTagForLogging">
    /// Short tag (e.g. "ApproveReject/{appointmentId}") that identifies
    /// the call site in log output. Optional.
    /// </param>
    public async Task AppendAsync(
        List<NotificationRecipient> recipients,
        string? contextTagForLogging = null)
    {
        var systemParameter = await _systemParameterRepository.GetCurrentTenantAsync();
        if (systemParameter == null || string.IsNullOrWhiteSpace(systemParameter.CcEmailIds))
        {
            return;
        }

        var existing = new HashSet<string>(
            recipients.Select(r => r.Email),
            StringComparer.OrdinalIgnoreCase);

        var ccAddresses = systemParameter.CcEmailIds
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(a => a.Trim())
            .Where(a => a.Length > 0);

        var added = 0;
        foreach (var address in ccAddresses)
        {
            if (existing.Add(address))
            {
                recipients.Add(new NotificationRecipient(
                    email: address,
                    role: RecipientRole.OfficeAdmin,
                    isRegistered: false));
                added++;
            }
        }

        if (added > 0)
        {
            _logger.LogDebug(
                "CcRecipientAppender: appended {Count} CC recipient(s) for {Context}.",
                added, contextTagForLogging ?? "(none)");
        }
    }
}
