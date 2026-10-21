using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Handlers;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities.Events;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Staff change the parties on an existing appointment, at any status, including Approved
/// (2026-10-21). Before this, a party added through "Edit details" got a link row but never the
/// appointment's party-email column, and that column is the ONLY thing the read guard and the
/// appointment list consult for an attorney or claim examiner (<see cref="AppointmentReadAccessGuard"/>
/// passes no link-row ids; <c>AppointmentVisibilityService</c> matches email + role). So the party
/// was emailed the packet and then could not open the case.
///
/// <para><b>STAFF ONLY, and that is the security property, not a convenience.</b> The party-email
/// columns decide who can open the case, so a caller who can write one can grant access to any
/// address it likes. This is the reason <c>AttorneySnapshot</c> deliberately never copies an
/// attorney master's email onto the appointment. Two gates, both required:
/// <list type="bullet">
/// <item><c>CaseEvaluation.Appointments.Edit</c>, which no external role holds;</item>
/// <item><see cref="BookingFlowRoles.IsInternalUserCaller"/>, so a misconfigured grant to an
/// external role still cannot reach the writes.</item>
/// </list>
/// External parties keep the read-only screen and the staff-initiated Send Back fix-it page.</para>
///
/// <para>The attorney master and link rows go through the SAME upserts booking uses
/// (<see cref="IAppointmentsAppService.UpsertApplicantAttorneyForAppointmentAsync"/> and the
/// defense twin), so the dedup-by-email, the snapshot capture in the link managers and the
/// sign-up auto-link behave exactly as for a booked attorney.</para>
///
/// <para><b>What follows a change</b> (<see cref="OnPartiesChangedAsync"/>):
/// <list type="bullet">
/// <item>The Case Tracker gets the updated intake through the same handler every post-approval
/// appointment edit uses, <see cref="AppointmentChangedHandler"/>. A claim-examiner-only edit
/// changes no column on the appointment row; today ABP still publishes the appointment's update
/// event, because the examiner row's foreign key points at it
/// (<c>AbpEntityChangeOptions.PublishEntityUpdatedEventWhenNavigationChanges</c>, on by default).
/// The handler is called explicitly as well, so the Case Tracker update does not hang on that
/// option; a test turns it off to prove this path. Both builds produce the identical payload and
/// the outbox collapses them onto one row.</item>
/// <item>A NEW party address on a live appointment gets the packet email it would have got at
/// approval. The packet emails are driven by <see cref="PacketGeneratedEto"/>, sent once per
/// address and de-duplicated on (office, address, appointment, packet kind), so re-publishing it
/// for an already-rendered packet mails only the addresses that were never mailed. The packet is
/// the one rendered at approval; it is not re-rendered.</item>
/// </list></para>
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.Appointments.Edit)]
public class AppointmentPartiesAppService : CaseEvaluationAppService, IAppointmentPartiesAppService
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IAppointmentsAppService _appointments;
    private readonly IAppointmentApplicantAttorneyRepository _applicantLinks;
    private readonly IAppointmentDefenseAttorneyRepository _defenseLinks;
    private readonly IApplicantAttorneyRepository _applicantMasters;
    private readonly IDefenseAttorneyRepository _defenseMasters;
    private readonly IRepository<AppointmentClaimExaminer, Guid> _claimExaminers;
    private readonly AppointmentReadAccessGuard _readAccessGuard;
    private readonly AppointmentChangedHandler _caseTrackerRePush;
    private readonly IRepository<AppointmentPacket, Guid> _packets;
    private readonly ILocalEventBus _localEventBus;

    /// <summary>
    /// The statuses on which a newly added party is sent the packet: the appointment is live and
    /// its parties were already mailed at approval. Before approval nothing is sent here, because
    /// approval itself mails everyone on the appointment at that point.
    /// </summary>
    internal static readonly AppointmentStatusType[] PacketResendStatuses =
    {
        AppointmentStatusType.Approved,
        AppointmentStatusType.RescheduleRequested,
        AppointmentStatusType.CancellationRequested,
    };

    public AppointmentPartiesAppService(
        IAppointmentRepository appointmentRepository,
        IAppointmentsAppService appointments,
        IAppointmentApplicantAttorneyRepository applicantLinks,
        IAppointmentDefenseAttorneyRepository defenseLinks,
        IApplicantAttorneyRepository applicantMasters,
        IDefenseAttorneyRepository defenseMasters,
        IRepository<AppointmentClaimExaminer, Guid> claimExaminers,
        AppointmentReadAccessGuard readAccessGuard,
        AppointmentChangedHandler caseTrackerRePush,
        IRepository<AppointmentPacket, Guid> packets,
        ILocalEventBus localEventBus)
    {
        _appointmentRepository = appointmentRepository;
        _appointments = appointments;
        _applicantLinks = applicantLinks;
        _defenseLinks = defenseLinks;
        _applicantMasters = applicantMasters;
        _defenseMasters = defenseMasters;
        _claimExaminers = claimExaminers;
        _readAccessGuard = readAccessGuard;
        _caseTrackerRePush = caseTrackerRePush;
        _packets = packets;
        _localEventBus = localEventBus;
    }

    public virtual async Task<AppointmentDto> UpdateAsync(Guid appointmentId, AppointmentPartiesUpdateDto input)
    {
        Check.NotNull(input, nameof(input));

        if (!BookingFlowRoles.IsInternalUserCaller(CurrentUser.Roles))
        {
            throw new AbpAuthorizationException("Only office staff can change the parties on an appointment.");
        }

        var appointment = await _appointmentRepository.GetAsync(appointmentId);
        await _readAccessGuard.EnsureCanReadAsync(appointment);

        var before = PartyEmails.Of(appointment);

        if (input.PatientEmail != null)
        {
            appointment.PatientEmail = RequireEmail(input.PatientEmail, "Patient");
        }

        await ApplyApplicantAttorneyAsync(appointment, input.ApplicantAttorney);
        await ApplyDefenseAttorneyAsync(appointment, input.DefenseAttorney);
        await ApplyClaimExaminerAsync(appointment, input.ClaimExaminer);

        await _appointmentRepository.UpdateAsync(appointment, autoSave: true);

        var touched = input.PatientEmail != null
            || input.ApplicantAttorney != null
            || input.DefenseAttorney != null
            || input.ClaimExaminer != null;
        if (touched)
        {
            await OnPartiesChangedAsync(appointment, PartyEmails.ChangedRoles(before, PartyEmails.Of(appointment)));
        }

        return ObjectMapper.Map<Appointment, AppointmentDto>(appointment);
    }

    /// <summary>
    /// The Case Tracker update, then the packet email for each NEW party address. See the class
    /// remarks for why each is done this way.
    /// </summary>
    protected virtual async Task OnPartiesChangedAsync(Appointment appointment, IReadOnlyList<PartyEmailChange> newAddresses)
    {
        await _caseTrackerRePush.HandleEventAsync(new EntityUpdatedEventData<Appointment>(appointment));

        if (newAddresses.Count == 0 || !PacketResendStatuses.Contains(appointment.AppointmentStatus))
        {
            return;
        }

        var kinds = new HashSet<PacketKind>();
        if (newAddresses.Any(c => c.Role == PartyEmailRole.Patient))
        {
            kinds.Add(PacketKind.Patient);
        }
        if (newAddresses.Any(c => c.Role != PartyEmailRole.Patient))
        {
            kinds.Add(PacketKind.AttorneyClaimExaminer);
        }

        // Only a packet that has finished rendering. One still rendering mails everyone on the
        // appointment, the new party included, when it finishes.
        var packets = await _packets.GetListAsync(p =>
            p.AppointmentId == appointment.Id && p.Status == PacketGenerationStatus.Generated);
        foreach (var packet in packets.Where(p => kinds.Contains(p.Kind)))
        {
            var eto = new PacketGeneratedEto
            {
                AppointmentId = appointment.Id,
                TenantId = appointment.TenantId,
                PacketId = packet.Id,
                Kind = packet.Kind,
                OccurredAt = Clock.Now,
            };
            // After the commit, as the render job does: the handlers re-read the appointment's
            // party columns, which must already hold the new address.
            var uow = UnitOfWorkManager.Current;
            if (uow != null)
            {
                uow.OnCompleted(() => PublishPacketEmailAsync(eto));
            }
            else
            {
                await PublishPacketEmailAsync(eto);
            }
        }
    }

    /// <summary>
    /// Best-effort. The party change is already committed when this runs, so a mail problem (a
    /// missing template, the mail queue down) is logged and must never turn a saved change into an
    /// error on the staff member's screen.
    /// </summary>
    private async Task PublishPacketEmailAsync(PacketGeneratedEto eto)
    {
        try
        {
            await _localEventBus.PublishAsync(eto);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(
                ex,
                "AppointmentPartiesAppService: the {Kind} packet email for a new party on appointment {AppointmentId} was not queued; the party change stands.",
                eto.Kind, eto.AppointmentId);
        }
    }

    private async Task ApplyApplicantAttorneyAsync(Appointment appointment, StaffAttorneyChangeDto? change)
    {
        if (change == null)
        {
            return;
        }

        if (change.Remove)
        {
            await _applicantLinks.DeleteAsync(l => l.AppointmentId == appointment.Id, autoSave: true);
            appointment.ApplicantAttorneyEmail = null;
            AttorneySnapshot.ClearApplicant(appointment);
            return;
        }

        var email = RequireEmail(change.Email, "ApplicantAttorney");
        var masterId = await KeepMasterIdOnlyForTheSameEmailAsync(
            change.AttorneyId, email, id => _applicantMasters.FindAsync(id), m => m.Email);

        await _appointments.UpsertApplicantAttorneyForAppointmentAsync(appointment.Id, new ApplicantAttorneyDetailsDto
        {
            ApplicantAttorneyId = masterId,
            // Empty on purpose: the upsert then resolves the account from the email, so an
            // attorney typed before registering links itself when they sign up with it.
            IdentityUserId = Guid.Empty,
            FirstName = change.FirstName ?? string.Empty,
            LastName = change.LastName ?? string.Empty,
            Email = email,
            FirmName = change.FirmName,
            WebAddress = change.WebAddress,
            PhoneNumber = change.PhoneNumber,
            FaxNumber = change.FaxNumber,
            Street = change.Street,
            City = change.City,
            StateId = change.StateId,
            ZipCode = change.ZipCode,
        });
        appointment.ApplicantAttorneyEmail = email;
    }

    private async Task ApplyDefenseAttorneyAsync(Appointment appointment, StaffAttorneyChangeDto? change)
    {
        if (change == null)
        {
            return;
        }

        if (change.Remove)
        {
            await _defenseLinks.DeleteAsync(l => l.AppointmentId == appointment.Id, autoSave: true);
            appointment.DefenseAttorneyEmail = null;
            AttorneySnapshot.ClearDefense(appointment);
            return;
        }

        var email = RequireEmail(change.Email, "DefenseAttorney");
        var masterId = await KeepMasterIdOnlyForTheSameEmailAsync(
            change.AttorneyId, email, id => _defenseMasters.FindAsync(id), m => m.Email);

        await _appointments.UpsertDefenseAttorneyForAppointmentAsync(appointment.Id, new DefenseAttorneyDetailsDto
        {
            DefenseAttorneyId = masterId,
            IdentityUserId = Guid.Empty,
            FirstName = change.FirstName ?? string.Empty,
            LastName = change.LastName ?? string.Empty,
            Email = email,
            FirmName = change.FirmName,
            WebAddress = change.WebAddress,
            PhoneNumber = change.PhoneNumber,
            FaxNumber = change.FaxNumber,
            Street = change.Street,
            City = change.City,
            StateId = change.StateId,
            ZipCode = change.ZipCode,
        });
        appointment.DefenseAttorneyEmail = email;
    }

    private async Task ApplyClaimExaminerAsync(Appointment appointment, StaffClaimExaminerChangeDto? change)
    {
        if (change == null)
        {
            return;
        }

        if (change.Remove)
        {
            await _claimExaminers.DeleteAsync(x => x.AppointmentId == appointment.Id, autoSave: true);
            appointment.ClaimExaminerEmail = null;
            return;
        }

        var email = RequireEmail(change.Email, "ClaimExaminer");
        var row = await _claimExaminers.FirstOrDefaultAsync(x => x.AppointmentId == appointment.Id);
        var isNew = row == null;
        row ??= new AppointmentClaimExaminer(GuidGenerator.Create(), appointment.Id, isActive: true)
        {
            TenantId = appointment.TenantId,
        };
        row.IsActive = true;
        row.Name = change.Name;
        row.Email = email;
        row.PhoneNumber = change.PhoneNumber;
        row.Fax = change.Fax;
        row.Suite = change.Suite;
        row.Street = change.Street;
        row.City = change.City;
        row.StateId = change.StateId;
        row.Zip = change.Zip;
        if (isNew)
        {
            await _claimExaminers.InsertAsync(row, autoSave: true);
        }
        else
        {
            await _claimExaminers.UpdateAsync(row, autoSave: true);
        }
        appointment.ClaimExaminerEmail = email;
    }

    /// <summary>
    /// The attorney record the page loaded is reused only while the email still matches it.
    /// Staff replacing attorney A with attorney B send A's id along with B's details; writing B
    /// into A's record would rename A everywhere A is linked, including other appointments.
    /// </summary>
    private static async Task<Guid?> KeepMasterIdOnlyForTheSameEmailAsync<TMaster>(
        Guid? masterId, string email, Func<Guid, Task<TMaster?>> find, Func<TMaster, string?> emailOf)
        where TMaster : class
    {
        if (!masterId.HasValue || masterId.Value == Guid.Empty)
        {
            return null;
        }

        var master = await find(masterId.Value);
        return master != null && string.Equals(emailOf(master)?.Trim(), email, StringComparison.OrdinalIgnoreCase)
            ? masterId
            : null;
    }

    private string RequireEmail(string? email, string partyKey)
    {
        var trimmed = email?.Trim();
        if (string.IsNullOrEmpty(trimmed) || !IsEmailAddress(trimmed))
        {
            throw new UserFriendlyException(L["AppointmentParties:EmailRequired", L[partyKey]]);
        }
        return trimmed;
    }

    private static bool IsEmailAddress(string value)
    {
        // The DTO's [EmailAddress] already refused a malformed address; this is the server's own
        // check for the value it is about to make an access key. MailAddress accepts display-name
        // forms ("A <a@b.c>"), so the parsed address must equal the input exactly.
        try
        {
            return new MailAddress(value).Address == value;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
