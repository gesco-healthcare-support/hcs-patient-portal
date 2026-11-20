using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.PhiAccess;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-11-06 (R2c) -- the earlier appointment a re-evaluation follows from, loaded by its
/// confirmation number and the patient's date of birth. Open to every caller who may book (the
/// office decided patients and all external parties may use it), including one who is not a party
/// to the earlier appointment: a new paralegal at the same firm, say.
///
/// <para><b>Who gets what.</b> The access rule is <see cref="ReEvaluationSourceAccess"/>. A party gets
/// what the per-appointment reads already gave them, Authorized Users included. A non-party gets
/// what the booking form needs to copy and nothing more: no Authorized Users, no patient or booker
/// login, and no Social Security number at all (a party sees its last four).</para>
///
/// <para><b>Logged.</b> A party's load is an <see cref="PhiAccessAction.AppointmentViewed"/>, as
/// the confirmation-number read records it. A non-party's is
/// <see cref="PhiAccessAction.ReEvaluationSourceLoadedByDateOfBirth"/>, and every refusal is
/// <see cref="PhiAccessAction.ReEvaluationSourceLookupRefused"/> with no patient or appointment named.</para>
///
/// <para><b>The newest case (2026-11-08, W2).</b> The answer is the newest re-evaluable case in the chain
/// the typed number belongs to, and "party" means a party to THAT case. <see cref="ReEvaluationSourceDto.FilledFromNewer"/>
/// tells the form when it is not the typed one, so it can say so. The booking form now loads every
/// re-evaluation through this call, a party's included; the date of birth is only needed by a caller
/// who is not a party to the newest case.</para>
///
/// <para>Not auto-exposed: the hand-written <c>ReEvaluationSourcesController</c> is the only route.</para>
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.Appointments.Create)]
public class ReEvaluationSourcesAppService : CaseEvaluationAppService, IReEvaluationSourcesAppService
{
    private readonly ReEvaluationSourceAccess _sourceAccess;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IAppointmentApplicantAttorneyRepository _applicantAttorneyLinks;
    private readonly IAppointmentDefenseAttorneyRepository _defenseAttorneyLinks;

    public ReEvaluationSourcesAppService(
        ReEvaluationSourceAccess sourceAccess,
        IAppointmentRepository appointmentRepository,
        IAppointmentApplicantAttorneyRepository applicantAttorneyLinks,
        IAppointmentDefenseAttorneyRepository defenseAttorneyLinks)
    {
        _sourceAccess = sourceAccess;
        _appointmentRepository = appointmentRepository;
        _applicantAttorneyLinks = applicantAttorneyLinks;
        _defenseAttorneyLinks = defenseAttorneyLinks;
    }

    public virtual async Task<ReEvaluationSourceDto?> LookupAsync(ReEvaluationSourceLookupDto input)
    {
        Check.NotNull(input, nameof(input));

        var match = await _sourceAccess.FindAsync(input.ConfirmationNumber, input.PatientDateOfBirth);

        // 2026-11-08 (W2): every re-evaluation load comes through here now, a party's included. A party
        // gets its case back whatever the status -- it could read it anyway -- so the booking form can
        // give the specific "only an approved appointment" refusal instead of a not-found. That case is
        // only ever not re-evaluable when nothing in its chain is. A caller who is not a party only ever
        // matches a case that can be re-evaluated; anything else is the same null for every outcome.
        var canBeReEvaluated = match != null
            && AppointmentLifecycleValidators.CanCreateReval(
                match.Source.AppointmentStatus, match.Source.EvaluationKind, CurrentUser.IsInRole("IT Admin"));
        if (match == null || (!match.CallerIsParty && !canBeReEvaluated))
        {
            await PhiAccessRecorder.RecordAsync(
                PhiAccessAction.ReEvaluationSourceLookupRefused, new PhiAccessTarget(null, null));
            return null;
        }

        var source = match.Source;
        var graph = await _appointmentRepository.GetWithNavigationPropertiesAsync(source.Id);
        var dto = ObjectMapper.Map<AppointmentWithNavigationProperties, AppointmentWithNavigationPropertiesDto>(graph!);
        ExternalUserDtoFilter.MaskInternalFields(dto, !BookingFlowRoles.IsInternalUserCaller(CurrentUser.Roles));

        if (match.CallerIsParty)
        {
            if (dto.Patient != null)
            {
                SsnVisibility.MaskToLast4(dto.Patient);
            }
        }
        else
        {
            WithholdFromNonParty(dto);
        }

        var applicant = await _applicantAttorneyLinks.GetListWithNavigationPropertiesAsync(
            appointmentId: source.Id, maxResultCount: 1);
        var defense = await _defenseAttorneyLinks.GetListWithNavigationPropertiesAsync(
            appointmentId: source.Id, maxResultCount: 1);

        await PhiAccessRecorder.RecordAsync(
            match.CallerIsParty
                ? PhiAccessAction.AppointmentViewed
                : PhiAccessAction.ReEvaluationSourceLoadedByDateOfBirth,
            new PhiAccessTarget(source.PatientId, source.Id));

        return new ReEvaluationSourceDto
        {
            Source = dto,
            ApplicantAttorney = AppointmentAttorneyDetails.From(applicant.FirstOrDefault()),
            DefenseAttorney = AppointmentAttorneyDetails.From(defense.FirstOrDefault()),
            CallerIsParty = match.CallerIsParty,
            RequestedConfirmationNumber = match.Requested.RequestConfirmationNumber,
            FilledFromNewer = match.FilledFromNewer,
            CanBeReEvaluated = canBeReEvaluated,
        };
    }

    /// <summary>
    /// What a non-party does not need to book the re-evaluation: who else may see the earlier case,
    /// the patient's and the booker's logins, and the Social Security number (the form never
    /// prefills it).
    /// </summary>
    private static void WithholdFromNonParty(AppointmentWithNavigationPropertiesDto dto)
    {
        dto.AppointmentAccessors = new();
        dto.IdentityUser = null;
        dto.BookedByUser = null;
        if (dto.Patient != null)
        {
            dto.Patient.SocialSecurityNumber = null;
        }
    }
}
