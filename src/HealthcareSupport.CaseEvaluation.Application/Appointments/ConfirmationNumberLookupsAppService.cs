using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.PhiAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-11-06 (R3) -- "I don't know the confirmation number". A booker gives the patient's name, date
/// of birth and claim number; when they match exactly one patient's case in this office, the number is
/// emailed to the booker's OWN verified login, and the office inbox is told who received it. Open to
/// every caller who may book (the office decided patients and all external parties may use it).
///
/// <para><b>The same answer every time</b>, like forgot-password: nothing is returned and nothing
/// thrown after the throttle, whether nothing matched, several patients matched, the caller has no
/// verified email, or the number was sent. Only the throttle refuses, and it is counted BEFORE the
/// match so it says nothing about matches either.</para>
///
/// <para><b>Logged</b> in the PHI access log: a sent number as <see cref="PhiAccessAction.ConfirmationNumberLookupMatched"/>
/// with its case, anything else as <see cref="PhiAccessAction.ConfirmationNumberLookupNotMatched"/>
/// with nothing named.</para>
///
/// <para>Not auto-exposed: the hand-written <c>ConfirmationNumberLookupsController</c> is the only route.</para>
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.Appointments.Create)]
public class ConfirmationNumberLookupsAppService : CaseEvaluationAppService, IConfirmationNumberLookupsAppService
{
    private readonly ReEvaluationLookupThrottle _throttle;
    private readonly ConfirmationNumberLookupMatcher _matcher;
    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly OfficeNotificationRecipients _officeRecipients;
    private readonly ILogger<ConfirmationNumberLookupsAppService> _logger;

    public ConfirmationNumberLookupsAppService(
        ReEvaluationLookupThrottle throttle,
        ConfirmationNumberLookupMatcher matcher,
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        OfficeNotificationRecipients officeRecipients,
        ILogger<ConfirmationNumberLookupsAppService> logger)
    {
        _throttle = throttle;
        _matcher = matcher;
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _officeRecipients = officeRecipients;
        _logger = logger;
    }

    public virtual async Task RequestAsync(ConfirmationNumberLookupDto input)
    {
        Check.NotNull(input, nameof(input));

        await _throttle.EnsureAllowedAndStampAsync(LookupThrottleBudget.ConfirmationNumber);

        // An unverified login proves nothing about who reads that mailbox, so it is treated as no match.
        var requesterEmail = CurrentUser.GetVerifiedEmail();
        var match = string.IsNullOrWhiteSpace(requesterEmail) ? null : await _matcher.FindAsync(input);
        if (match == null)
        {
            await PhiAccessRecorder.RecordAsync(
                PhiAccessAction.ConfirmationNumberLookupNotMatched, new PhiAccessTarget(null, null));
            return;
        }

        await SendAsync(match, requesterEmail!);
        await PhiAccessRecorder.RecordAsync(
            PhiAccessAction.ConfirmationNumberLookupMatched, new PhiAccessTarget(match.PatientId, match.Id));
    }

    private async Task SendAsync(Appointment appointment, string requesterEmail)
    {
        var ctx = await _contextResolver.ResolveAsync(appointment.Id, null);
        var variables = new Dictionary<string, object?>(
            DocumentNotificationContext.BuildVariables(
                patientFirstName: ctx?.PatientFirstName,
                patientLastName: ctx?.PatientLastName,
                patientEmail: null,
                requestConfirmationNumber: appointment.RequestConfirmationNumber,
                appointmentDate: appointment.AppointmentDate,
                claimNumber: null,
                wcabAdj: null,
                documentName: null,
                rejectionNotes: null,
                // Filled by NotificationTemplateRenderer from the tenant store (#1014).
                clinicName: null,
                portalUrl: ctx?.PortalBaseUrl))
        {
            ["RequesterName"] = $"{CurrentUser.Name} {CurrentUser.SurName}".Trim(),
            ["RequesterEmail"] = requesterEmail,
        };

        try
        {
            // Each request is its own email: a second lookup for the same case must send again.
            await _dispatcher.DispatchAsync(
                notificationCase: NotificationCase.Of(appointment.Id),
                templateCode: NotificationTemplateConsts.Codes.ConfirmationNumberLookup,
                recipients: new[] { new NotificationRecipient(email: requesterEmail, isRegistered: true) },
                variables: variables,
                contextTag: NotificationOccurrence.PerRequest($"ConfirmationNumberLookup/{appointment.Id}"));

            // The tag passed here only labels the "no office inbox" warning.
            var inbox = await _officeRecipients.GetOfficeInboxAsync($"ConfirmationNumberLookup/Office/{appointment.Id}");
            if (inbox.Count > 0)
            {
                await _dispatcher.DispatchAsync(
                    notificationCase: NotificationCase.Of(appointment.Id),
                    templateCode: NotificationTemplateConsts.Codes.ConfirmationNumberLookupOffice,
                    recipients: inbox,
                    variables: variables,
                    contextTag: NotificationOccurrence.PerRequest($"ConfirmationNumberLookup/Office/{appointment.Id}"));
            }
        }
        catch (BusinessException ex) when (ex.Code == CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound)
        {
            // Until the migrator seeds the two templates, the answer must still be the same one.
            _logger.LogError(
                ex,
                "ConfirmationNumberLookupsAppService: a template is missing or switched off; the confirmation number for appointment {AppointmentId} was not sent.",
                appointment.Id);
        }
    }
}
