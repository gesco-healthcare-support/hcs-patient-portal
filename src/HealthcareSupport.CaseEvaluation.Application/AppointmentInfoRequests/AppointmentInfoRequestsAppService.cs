using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentLanguages;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// Send Back / Request-more-information (2026-06-14). Staff flag fields + add a
/// note (Pending -&gt; InfoRequested); the external party resubmits their
/// corrections (InfoRequested -&gt; Pending). The note + flagged-field list are
/// returned un-masked so the external fix-it page can render them. The flagged
/// fields are stored as a JSON array, so DTO mapping is manual (Mapperly cannot
/// deserialize JSON).
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize]
public class AppointmentInfoRequestsAppService
    : CaseEvaluationAppService, IAppointmentInfoRequestsAppService
{
    private readonly IRepository<AppointmentInfoRequest, Guid> _infoRequestRepository;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly AppointmentManager _appointmentManager;
    private readonly AppointmentReadAccessGuard _readAccessGuard;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IRepository<AppointmentPrimaryInsurance, Guid> _insuranceRepository;
    private readonly IRepository<AppointmentDefenseAttorney, Guid> _defenseLinkRepository;
    private readonly IRepository<DefenseAttorney, Guid> _defenseRepository;
    private readonly IRepository<AppointmentLanguage, Guid> _languageRepository;
    private readonly IIdentityUserRepository _userRepository;

    public AppointmentInfoRequestsAppService(
        IRepository<AppointmentInfoRequest, Guid> infoRequestRepository,
        IAppointmentRepository appointmentRepository,
        AppointmentManager appointmentManager,
        AppointmentReadAccessGuard readAccessGuard,
        IRepository<Patient, Guid> patientRepository,
        IRepository<AppointmentPrimaryInsurance, Guid> insuranceRepository,
        IRepository<AppointmentDefenseAttorney, Guid> defenseLinkRepository,
        IRepository<DefenseAttorney, Guid> defenseRepository,
        IRepository<AppointmentLanguage, Guid> languageRepository,
        IIdentityUserRepository userRepository)
    {
        _infoRequestRepository = infoRequestRepository;
        _appointmentRepository = appointmentRepository;
        _appointmentManager = appointmentManager;
        _readAccessGuard = readAccessGuard;
        _patientRepository = patientRepository;
        _insuranceRepository = insuranceRepository;
        _defenseLinkRepository = defenseLinkRepository;
        _defenseRepository = defenseRepository;
        _languageRepository = languageRepository;
        _userRepository = userRepository;
    }

    [Authorize(CaseEvaluationPermissions.Appointments.Approve)]
    public virtual async Task<AppointmentInfoRequestDto> SendBackAsync(
        Guid appointmentId,
        SendBackAppointmentInput input)
    {
        if (appointmentId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Appointment"]]);
        }
        if (input == null || string.IsNullOrWhiteSpace(input.Note))
        {
            throw new UserFriendlyException(L["AppointmentInfoRequest:NoteRequired"]);
        }

        var appointment = await _appointmentRepository.GetAsync(appointmentId);
        if (appointment.AppointmentStatus != AppointmentStatusType.Pending)
        {
            throw new UserFriendlyException(L["AppointmentInfoRequest:OnlyPendingCanBeSentBack"]);
        }

        var fieldsJson = JsonSerializer.Serialize(input.FlaggedFields ?? new List<FlaggedFieldDto>());
        var entity = new AppointmentInfoRequest(
            GuidGenerator.Create(),
            appointment.TenantId,
            appointmentId,
            input.Note.Trim(),
            fieldsJson,
            CurrentUser.Id);

        // Snapshot the flagged fields' CURRENT values so the staff diff has a "before".
        entity.CaptureBeforeValues(await BuildSnapshotJsonAsync(appointment, ReadFlaggedKeys(entity)));
        await _infoRequestRepository.InsertAsync(entity, autoSave: true);

        // Fire the Pending -> InfoRequested transition (re-validates the source
        // status + publishes the status-changed event for notifications).
        await _appointmentManager.SendBackAsync(appointmentId, CurrentUser.Id);

        return MapToDto(entity);
    }

    [Authorize]
    public virtual async Task ResubmitAsync(Guid appointmentId)
    {
        // Party-scoped: the creator, internal staff, or an Edit-accessor. The
        // external user's corrected field values are saved through the existing
        // patient / document endpoints before this transition-only call.
        await _readAccessGuard.EnsureCanEditAsync(appointmentId);

        var open = await GetOpenEntityAsync(appointmentId);
        if (open != null)
        {
            // Snapshot the corrected values so the staff diff has an "after".
            var appointment = await _appointmentRepository.GetAsync(appointmentId);
            open.CaptureAfterValues(await BuildSnapshotJsonAsync(appointment, ReadFlaggedKeys(open)));
            open.MarkResolved(Clock.Now);
            await _infoRequestRepository.UpdateAsync(open, autoSave: true);
        }

        await _appointmentManager.ResubmitInfoAsync(appointmentId, CurrentUser.Id);
    }

    [Authorize]
    public virtual async Task SaveCorrectionsAsync(
        Guid appointmentId,
        SaveInfoRequestCorrectionsInput input)
    {
        Check.NotNull(input, nameof(input));

        // Same trust boundary as ResubmitAsync: creator / internal staff / Edit-accessor.
        await _readAccessGuard.EnsureCanEditAsync(appointmentId);

        var appointment = await _appointmentRepository.GetAsync(appointmentId);
        if (appointment.AppointmentStatus != AppointmentStatusType.InfoRequested)
        {
            throw new UserFriendlyException(L["AppointmentInfoRequest:OnlyInfoRequestedCanBeCorrected"]);
        }

        var open = await GetOpenEntityAsync(appointmentId);
        if (open == null)
        {
            throw new UserFriendlyException(L["AppointmentInfoRequest:NoOpenRequest"]);
        }

        // Server-side lock: a correction may only touch fields staff flagged.
        var flaggedKeys = ReadFlaggedKeys(open);
        if (InfoRequestCorrectionLock.FindUnflaggedChanges(input, flaggedKeys).Count > 0)
        {
            throw new UserFriendlyException(L["AppointmentInfoRequest:OnlyFlaggedFieldsCanBeChanged"]);
        }

        await ApplyPatientCorrectionsAsync(appointment.PatientId, input);
        await ApplyAppointmentEmailCorrectionsAsync(appointment, input);
        await ApplyInsuranceCorrectionsAsync(appointmentId, input);
        await ApplyDefenseFirmCorrectionAsync(appointmentId, input);
    }

    private static HashSet<string> ReadFlaggedKeys(AppointmentInfoRequest entity)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var fields = JsonSerializer.Deserialize<List<FlaggedFieldDto>>(entity.RequestedFields);
            if (fields != null)
            {
                foreach (var field in fields)
                {
                    if (!string.IsNullOrWhiteSpace(field.Key))
                    {
                        keys.Add(field.Key!);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // A corrupt JSON blob yields an empty set, which locks every field -- safe.
        }
        return keys;
    }

    private static readonly string[] PatientSnapshotKeys =
    {
        "dateOfBirth", "socialSecurityNumber", "address", "cellPhoneNumber", "appointmentLanguageId",
    };

    /// <summary>
    /// Reads the CURRENT values of the flagged scalar fields from their homes and
    /// serializes the masked/formatted snapshot. Only the homes a flagged key needs
    /// are queried. Used for the send-back "before" + resubmit "after" captures.
    /// </summary>
    private async Task<string> BuildSnapshotJsonAsync(Appointment appointment, ISet<string> flaggedKeys)
    {
        DateTime? dob = null;
        string? ssn = null, address = null, cell = null, languageName = null, insuranceName = null, defenseFirm = null;

        if (flaggedKeys.Overlaps(PatientSnapshotKeys))
        {
            var patient = await _patientRepository.FindAsync(appointment.PatientId);
            if (patient != null)
            {
                dob = patient.DateOfBirth;
                ssn = patient.SocialSecurityNumber;
                address = patient.Address;
                cell = patient.CellPhoneNumber;
                if (flaggedKeys.Contains("appointmentLanguageId") && patient.AppointmentLanguageId is Guid languageId)
                {
                    languageName = (await _languageRepository.FindAsync(languageId))?.Name;
                }
            }
        }

        if (flaggedKeys.Contains("appointmentInsuranceName"))
        {
            insuranceName = (await _insuranceRepository
                .FirstOrDefaultAsync(x => x.AppointmentId == appointment.Id))?.Name;
        }

        if (flaggedKeys.Contains("defenseAttorneyFirmName"))
        {
            var link = await _defenseLinkRepository
                .FirstOrDefaultAsync(x => x.AppointmentId == appointment.Id);
            if (link != null)
            {
                defenseFirm = (await _defenseRepository.FindAsync(link.DefenseAttorneyId))?.FirmName;
            }
        }

        var values = new InfoRequestSnapshot.FieldValues
        {
            DateOfBirth = dob,
            SocialSecurityNumber = ssn,
            Address = address,
            CellPhoneNumber = cell,
            AppointmentLanguageName = languageName,
            ApplicantAttorneyEmail = appointment.ApplicantAttorneyEmail,
            ClaimExaminerEmail = appointment.ClaimExaminerEmail,
            InsuranceName = insuranceName,
            DefenseAttorneyFirmName = defenseFirm,
        };

        return InfoRequestSnapshot.Serialize(InfoRequestSnapshot.Capture(values, flaggedKeys));
    }

    /// <summary>
    /// Applies the flagged patient-demographic corrections directly on the Patient
    /// entity. Direct repository write (the endpoint is the trust boundary) -- this
    /// avoids the booking patient endpoint, which deliberately ignores DateOfBirth,
    /// and its permission gating. Only PROVIDED fields are written; SSN is stored
    /// raw as typed (reads remain masked elsewhere).
    /// </summary>
    private async Task ApplyPatientCorrectionsAsync(Guid patientId, SaveInfoRequestCorrectionsInput input)
    {
        var touchesPatient = input.DateOfBirth.HasValue
            || input.SocialSecurityNumber != null
            || input.Address != null
            || input.CellPhoneNumber != null
            || input.AppointmentLanguageId.HasValue;
        if (!touchesPatient)
        {
            return;
        }

        var patient = await _patientRepository.FindAsync(patientId);
        if (patient == null)
        {
            return;
        }

        if (input.DateOfBirth.HasValue)
        {
            patient.DateOfBirth = input.DateOfBirth.Value;
        }
        if (input.SocialSecurityNumber != null)
        {
            patient.SocialSecurityNumber = input.SocialSecurityNumber;
        }
        if (input.Address != null)
        {
            patient.Address = input.Address;
        }
        if (input.CellPhoneNumber != null)
        {
            patient.CellPhoneNumber = input.CellPhoneNumber;
        }
        if (input.AppointmentLanguageId.HasValue)
        {
            patient.AppointmentLanguageId = input.AppointmentLanguageId;
        }
        await _patientRepository.UpdateAsync(patient, autoSave: true);
    }

    private async Task ApplyAppointmentEmailCorrectionsAsync(
        Appointment appointment, SaveInfoRequestCorrectionsInput input)
    {
        var changed = false;
        if (input.ApplicantAttorneyEmail != null)
        {
            appointment.ApplicantAttorneyEmail = input.ApplicantAttorneyEmail;
            changed = true;
        }
        if (input.ClaimExaminerEmail != null)
        {
            appointment.ClaimExaminerEmail = input.ClaimExaminerEmail;
            changed = true;
        }
        if (changed)
        {
            await _appointmentRepository.UpdateAsync(appointment, autoSave: true);
        }
    }

    private async Task ApplyInsuranceCorrectionsAsync(
        Guid appointmentId, SaveInfoRequestCorrectionsInput input)
    {
        if (input.InsuranceName == null && input.InsurancePhoneNumber == null)
        {
            return;
        }

        // Direct repository write: the corrections endpoint is the trust boundary
        // (EnsureCanEditAsync above), so it must NOT route through the permission-gated
        // AppointmentPrimaryInsurances app service (external roles lack its grants).
        var row = await _insuranceRepository.FirstOrDefaultAsync(x => x.AppointmentId == appointmentId);
        if (row == null)
        {
            return;
        }

        if (input.InsuranceName != null)
        {
            row.Name = input.InsuranceName;
        }
        if (input.InsurancePhoneNumber != null)
        {
            row.PhoneNumber = input.InsurancePhoneNumber;
        }
        await _insuranceRepository.UpdateAsync(row, autoSave: true);
    }

    private async Task ApplyDefenseFirmCorrectionAsync(
        Guid appointmentId, SaveInfoRequestCorrectionsInput input)
    {
        if (input.DefenseAttorneyFirmName == null)
        {
            return;
        }

        var link = await _defenseLinkRepository.FirstOrDefaultAsync(x => x.AppointmentId == appointmentId);
        if (link == null)
        {
            return;
        }

        // FirmName lives on the DefenseAttorney master (the record the booking form
        // edits). Direct repository write -- the endpoint already authorized via
        // EnsureCanEditAsync, so it bypasses the gated DefenseAttorneys app service.
        var master = await _defenseRepository.FindAsync(link.DefenseAttorneyId);
        if (master == null)
        {
            return;
        }
        master.FirmName = input.DefenseAttorneyFirmName;
        await _defenseRepository.UpdateAsync(master, autoSave: true);
    }

    [Authorize]
    public virtual async Task<AppointmentInfoRequestDto?> GetOpenAsync(Guid appointmentId)
    {
        await _readAccessGuard.EnsureCanReadAsync(appointmentId);
        var open = await GetOpenEntityAsync(appointmentId);
        return open == null ? null : MapToDto(open);
    }

    [Authorize]
    public virtual async Task<List<AppointmentInfoRequestRoundDto>> GetHistoryAsync(Guid appointmentId)
    {
        await _readAccessGuard.EnsureCanReadAsync(appointmentId);

        var rows = await _infoRequestRepository.GetListAsync(r => r.AppointmentId == appointmentId);
        var ordered = rows.OrderBy(r => r.CreationTime).ToList();

        var nameCache = new Dictionary<Guid, string?>();
        var result = new List<AppointmentInfoRequestRoundDto>(ordered.Count);

        for (var i = 0; i < ordered.Count; i++)
        {
            var entity = ordered[i];
            var diffs = InfoRequestSnapshot.BuildDiff(
                InfoRequestSnapshot.Deserialize(entity.BeforeValues),
                InfoRequestSnapshot.Deserialize(entity.AfterValues),
                ReadFlaggedKeys(entity));
            var resolved = entity.Status == InfoRequestStatus.Resolved;

            result.Add(new AppointmentInfoRequestRoundDto
            {
                Id = entity.Id,
                RoundNumber = i + 1,
                Note = entity.Note,
                RequestedByName = await ResolveNameAsync(entity.RequestedByUserId, nameCache),
                RequestedAt = entity.CreationTime,
                IsResolved = resolved,
                ResolvedAt = entity.ResolvedAt,
                ResubmittedByName = resolved ? await ResolveNameAsync(entity.LastModifierId, nameCache) : null,
                FlaggedCount = diffs.Count,
                FixedCount = diffs.Count(d => d.Changed),
                Diffs = diffs,
            });
        }

        result.Reverse(); // newest-first for display
        return result;
    }

    /// <summary>Resolves an identity user id to a display name, caching within the call.</summary>
    private async Task<string?> ResolveNameAsync(Guid? userId, IDictionary<Guid, string?> cache)
    {
        if (userId is not Guid id)
        {
            return null;
        }
        if (cache.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var user = await _userRepository.FindAsync(id);
        string? name = null;
        if (user != null)
        {
            var full = $"{user.Name} {user.Surname}".Trim();
            name = string.IsNullOrWhiteSpace(full) ? (user.UserName ?? user.Email) : full;
        }
        cache[id] = name;
        return name;
    }

    private async Task<AppointmentInfoRequest?> GetOpenEntityAsync(Guid appointmentId)
    {
        return await _infoRequestRepository.FirstOrDefaultAsync(
            r => r.AppointmentId == appointmentId && r.Status == InfoRequestStatus.Open);
    }

    private static AppointmentInfoRequestDto MapToDto(AppointmentInfoRequest e)
    {
        List<FlaggedFieldDto> fields;
        try
        {
            fields = JsonSerializer.Deserialize<List<FlaggedFieldDto>>(e.RequestedFields)
                     ?? new List<FlaggedFieldDto>();
        }
        catch (JsonException)
        {
            fields = new List<FlaggedFieldDto>();
        }

        return new AppointmentInfoRequestDto
        {
            Id = e.Id,
            AppointmentId = e.AppointmentId,
            Note = e.Note,
            FlaggedFields = fields,
            Status = e.Status,
            RequestedByUserId = e.RequestedByUserId,
            CreationTime = e.CreationTime,
            ResolvedAt = e.ResolvedAt,
        };
    }
}
