using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.AppointmentLanguages;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.States;
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
    private readonly IRepository<AppointmentEmployerDetail, Guid> _employerRepository;
    private readonly IRepository<AppointmentClaimExaminer, Guid> _claimExaminerRepository;
    private readonly IRepository<AppointmentLanguage, Guid> _languageRepository;
    private readonly IRepository<State, Guid> _stateRepository;
    private readonly IRepository<AppointmentDocument, Guid> _documentRepository;
    private readonly IIdentityUserRepository _userRepository;

    public AppointmentInfoRequestsAppService(
        IRepository<AppointmentInfoRequest, Guid> infoRequestRepository,
        IAppointmentRepository appointmentRepository,
        AppointmentManager appointmentManager,
        AppointmentReadAccessGuard readAccessGuard,
        IRepository<Patient, Guid> patientRepository,
        IRepository<AppointmentPrimaryInsurance, Guid> insuranceRepository,
        IRepository<AppointmentEmployerDetail, Guid> employerRepository,
        IRepository<AppointmentClaimExaminer, Guid> claimExaminerRepository,
        IRepository<AppointmentLanguage, Guid> languageRepository,
        IRepository<State, Guid> stateRepository,
        IRepository<AppointmentDocument, Guid> documentRepository,
        IIdentityUserRepository userRepository)
    {
        _infoRequestRepository = infoRequestRepository;
        _appointmentRepository = appointmentRepository;
        _appointmentManager = appointmentManager;
        _readAccessGuard = readAccessGuard;
        _patientRepository = patientRepository;
        _insuranceRepository = insuranceRepository;
        _employerRepository = employerRepository;
        _claimExaminerRepository = claimExaminerRepository;
        _languageRepository = languageRepository;
        _stateRepository = stateRepository;
        _documentRepository = documentRepository;
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
            var appointment = await _appointmentRepository.GetAsync(appointmentId);
            var flaggedKeys = ReadFlaggedKeys(open);

            // F-018 fix (2026-06-23): the fix-it page disables Resubmit until every flagged field
            // is addressed, but that gate is client-side only (it keys off in-session `touched`),
            // so a direct API call could resubmit an un-fixed appointment. Re-check server-side
            // that each flagged field now holds a value (and that a document exists when documents
            // were flagged) before allowing the InfoRequested -> Pending transition.
            var unresolved = await GetUnresolvedFlaggedKeysAsync(appointment, flaggedKeys);
            if (unresolved.Count > 0)
            {
                throw new UserFriendlyException(
                    "Please complete all the requested corrections before resubmitting to the clinic.");
            }

            // Snapshot the corrected values so the staff diff has an "after".
            open.CaptureAfterValues(await BuildSnapshotJsonAsync(appointment, flaggedKeys));
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

        // A null/empty value means "no change", so only provided known keys are applied.
        var provided = (input.Corrections ?? new Dictionary<string, string?>())
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value) && InfoRequestFields.ByKey.ContainsKey(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

        // Server-side lock: a correction may only touch fields staff flagged.
        var flaggedKeys = ReadFlaggedKeys(open);
        if (InfoRequestCorrectionLock.FindUnflaggedChanges(provided.Keys, flaggedKeys).Count > 0)
        {
            throw new UserFriendlyException(L["AppointmentInfoRequest:OnlyFlaggedFieldsCanBeChanged"]);
        }
        if (provided.Count == 0)
        {
            return;
        }

        // Generic apply: load (or create) each owning entity, write each flagged value
        // through its registry descriptor, then persist the touched owners.
        var bundle = await BuildCorrectionBundleAsync(appointment, provided.Keys, createIfAbsent: true);
        var touchedOwners = new HashSet<InfoRequestFieldOwner>();
        foreach (var (key, raw) in provided)
        {
            var spec = InfoRequestFields.ByKey[key];
            spec.Write(bundle, raw);
            touchedOwners.Add(spec.Owner);
        }
        await SaveBundleAsync(bundle, touchedOwners);
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

    /// <summary>
    /// Reads the CURRENT values of the flagged scalar fields from their homes (via the
    /// field registry) and serializes the masked/formatted snapshot for the send-back
    /// "before" + resubmit "after" captures. Select ids (state / language) are resolved
    /// to display names; SSN masking + date formatting come from the registry.
    /// </summary>
    private async Task<string> BuildSnapshotJsonAsync(Appointment appointment, ISet<string> flaggedKeys)
    {
        var bundle = await BuildCorrectionBundleAsync(appointment, flaggedKeys, createIfAbsent: false);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in flaggedKeys)
        {
            if (!InfoRequestFields.ByKey.TryGetValue(key, out var spec))
            {
                continue; // documents + any non-scalar key are excluded from the value diff
            }
            map[key] = await ResolveDisplayValueAsync(spec, bundle);
        }
        return InfoRequestSnapshot.Serialize(map);
    }

    /// <summary>Registry value with state/language ids resolved to their display names.</summary>
    private async Task<string> ResolveDisplayValueAsync(InfoRequestFieldSpec spec, CorrectionBundle bundle)
    {
        var raw = spec.Read(bundle);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }
        if (spec.Kind == InfoRequestFieldKind.StateId && Guid.TryParse(raw, out var stateId))
        {
            return (await _stateRepository.FindAsync(stateId))?.Name ?? raw;
        }
        if (spec.Kind == InfoRequestFieldKind.LanguageId && Guid.TryParse(raw, out var languageId))
        {
            return (await _languageRepository.FindAsync(languageId))?.Name ?? raw;
        }
        return raw;
    }

    /// <summary>
    /// F-018 / L (2026-06-30): server-side check that the staff-flagged fields were
    /// actually addressed before a resubmit. FAIL-CLOSED: a flagged scalar key is
    /// unresolved unless it now holds a value (per its registry descriptor), so an
    /// unknown or not-yet-fixed field blocks the resubmit rather than passing silently.
    /// The "documents" key requires at least one uploaded document.
    /// </summary>
    private async Task<List<string>> GetUnresolvedFlaggedKeysAsync(Appointment appointment, ISet<string> flaggedKeys)
    {
        var unresolved = new List<string>();
        if (flaggedKeys.Count == 0)
        {
            return unresolved;
        }

        var bundle = await BuildCorrectionBundleAsync(appointment, flaggedKeys, createIfAbsent: false);
        foreach (var key in flaggedKeys)
        {
            if (key == "documents")
            {
                if (await _documentRepository.CountAsync(x => x.AppointmentId == appointment.Id) == 0)
                {
                    unresolved.Add(key);
                }
                continue;
            }

            if (!InfoRequestFields.ByKey.TryGetValue(key, out var spec)
                || string.IsNullOrWhiteSpace(spec.Read(bundle)))
            {
                unresolved.Add(key);
            }
        }

        return unresolved;
    }

    /// <summary>
    /// Loads the entities that own the given flagged/provided keys. The Appointment is
    /// always present; Patient is loaded when any patient field is involved; the linked
    /// Employer / Insurance / ClaimExaminer rows are loaded on demand and -- when
    /// <paramref name="createIfAbsent"/> -- created in memory so a flagged field on an
    /// appointment that never captured that section can still be filled in.
    /// </summary>
    private async Task<CorrectionBundle> BuildCorrectionBundleAsync(
        Appointment appointment, IEnumerable<string> keys, bool createIfAbsent)
    {
        var owners = keys
            .Where(InfoRequestFields.ByKey.ContainsKey)
            .Select(k => InfoRequestFields.ByKey[k].Owner)
            .ToHashSet();

        var bundle = new CorrectionBundle { Appointment = appointment };

        if (owners.Contains(InfoRequestFieldOwner.Patient))
        {
            bundle.Patient = await _patientRepository.FindAsync(appointment.PatientId);
        }

        if (owners.Contains(InfoRequestFieldOwner.Employer))
        {
            bundle.Employer = await _employerRepository.FirstOrDefaultAsync(x => x.AppointmentId == appointment.Id);
            if (bundle.Employer == null && createIfAbsent)
            {
                bundle.Employer = new AppointmentEmployerDetail(
                    GuidGenerator.Create(), appointment.Id, null, string.Empty, string.Empty);
                bundle.EmployerIsNew = true;
            }
        }

        if (owners.Contains(InfoRequestFieldOwner.Insurance))
        {
            bundle.Insurance = await _insuranceRepository.FirstOrDefaultAsync(x => x.AppointmentId == appointment.Id);
            if (bundle.Insurance == null && createIfAbsent)
            {
                bundle.Insurance = new AppointmentPrimaryInsurance(GuidGenerator.Create(), appointment.Id, true);
                bundle.InsuranceIsNew = true;
            }
        }

        if (owners.Contains(InfoRequestFieldOwner.ClaimExaminer))
        {
            bundle.ClaimExaminer = await _claimExaminerRepository.FirstOrDefaultAsync(x => x.AppointmentId == appointment.Id);
            if (bundle.ClaimExaminer == null && createIfAbsent)
            {
                bundle.ClaimExaminer = new AppointmentClaimExaminer(GuidGenerator.Create(), appointment.Id, true);
                bundle.ClaimExaminerIsNew = true;
            }
        }

        return bundle;
    }

    /// <summary>
    /// Persists the entities a correction touched. Direct repository writes: the
    /// corrections endpoint is the trust boundary (EnsureCanEditAsync), so it must NOT
    /// route through the permission-gated per-entity app services (external roles lack
    /// their grants). New linked rows are inserted; existing rows updated.
    /// </summary>
    private async Task SaveBundleAsync(CorrectionBundle bundle, ISet<InfoRequestFieldOwner> touchedOwners)
    {
        if (touchedOwners.Contains(InfoRequestFieldOwner.Patient) && bundle.Patient != null)
        {
            await _patientRepository.UpdateAsync(bundle.Patient, autoSave: true);
        }
        if (touchedOwners.Contains(InfoRequestFieldOwner.Appointment))
        {
            await _appointmentRepository.UpdateAsync(bundle.Appointment, autoSave: true);
        }
        if (touchedOwners.Contains(InfoRequestFieldOwner.Employer) && bundle.Employer != null)
        {
            await PersistAsync(_employerRepository, bundle.Employer, bundle.EmployerIsNew);
        }
        if (touchedOwners.Contains(InfoRequestFieldOwner.Insurance) && bundle.Insurance != null)
        {
            await PersistAsync(_insuranceRepository, bundle.Insurance, bundle.InsuranceIsNew);
        }
        if (touchedOwners.Contains(InfoRequestFieldOwner.ClaimExaminer) && bundle.ClaimExaminer != null)
        {
            await PersistAsync(_claimExaminerRepository, bundle.ClaimExaminer, bundle.ClaimExaminerIsNew);
        }
    }

    private static async Task PersistAsync<TEntity>(IRepository<TEntity, Guid> repository, TEntity entity, bool isNew)
        where TEntity : class, Volo.Abp.Domain.Entities.IEntity<Guid>
    {
        if (isNew)
        {
            await repository.InsertAsync(entity, autoSave: true);
        }
        else
        {
            await repository.UpdateAsync(entity, autoSave: true);
        }
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
