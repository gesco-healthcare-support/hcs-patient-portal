using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentBodyParts;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.CustomFields;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Guids;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// EF implementation of <see cref="IAppointmentChildCascadeCopier"/> (phase 4d, 2026-08-05).
///
/// <para>Rows are cloned through <see cref="PropertyValues.SetValues(object)"/>, which copies EVERY
/// mapped scalar. That is deliberate and is the whole defence against bug F18's failure mode: a
/// hand-written property list silently loses any column added to a child entity afterwards, and
/// nothing would fail. With <c>CurrentValues</c>, a new column is carried without touching this
/// file.</para>
///
/// <para>ORDER MATTERS. Injury details are copied before body parts, because a body part's FK is
/// <see cref="AppointmentBodyPart.AppointmentInjuryDetailId"/> -- it hangs off the injury detail,
/// NOT off the appointment. A "copy every table with an AppointmentId" loop misses body parts
/// entirely, which is exactly the shape of the original bug.</para>
/// </summary>
public class AppointmentChildCascadeCopier : IAppointmentChildCascadeCopier, ITransientDependency
{
    private readonly IDbContextProvider<CaseEvaluationDbContext> _dbContextProvider;
    private readonly IGuidGenerator _guidGenerator;

    public AppointmentChildCascadeCopier(
        IDbContextProvider<CaseEvaluationDbContext> dbContextProvider,
        IGuidGenerator guidGenerator)
    {
        _dbContextProvider = dbContextProvider;
        _guidGenerator = guidGenerator;
    }

    public virtual async Task<CopiedGroupCounts> CopyAllAsync(
        Guid sourceAppointmentId,
        Guid targetAppointmentId,
        Guid? tenantId)
    {
        var db = await _dbContextProvider.GetDbContextAsync();

        var accessors = await CopyDirectChildrenAsync<AppointmentAccessor>(db, sourceAppointmentId, targetAppointmentId, tenantId);
        var applicantAttorneys = await CopyDirectChildrenAsync<AppointmentApplicantAttorney>(db, sourceAppointmentId, targetAppointmentId, tenantId);
        var defenseAttorneys = await CopyDirectChildrenAsync<AppointmentDefenseAttorney>(db, sourceAppointmentId, targetAppointmentId, tenantId);
        var claimExaminers = await CopyDirectChildrenAsync<AppointmentClaimExaminer>(db, sourceAppointmentId, targetAppointmentId, tenantId);
        var employerDetails = await CopyDirectChildrenAsync<AppointmentEmployerDetail>(db, sourceAppointmentId, targetAppointmentId, tenantId);
        var primaryInsurances = await CopyDirectChildrenAsync<AppointmentPrimaryInsurance>(db, sourceAppointmentId, targetAppointmentId, tenantId);
        var customFieldValues = await CopyDirectChildrenAsync<CustomFieldValue>(db, sourceAppointmentId, targetAppointmentId, tenantId);
        // Documents share the MinIO object: BlobName is copied like any other scalar, so the new
        // row points at the SAME blob. Consequence already accepted by the epic -- delete becomes
        // soft-delete-only for shared blobs, matching the Case Tracker retention guarantee.
        var documents = await CopyDirectChildrenAsync<AppointmentDocument>(db, sourceAppointmentId, targetAppointmentId, tenantId);

        // Injury details FIRST, keeping old-id -> new-id so their body parts can be re-pointed.
        var injuryIdMap = new Dictionary<Guid, Guid>();
        var injuryDetails = await CopyDirectChildrenAsync<AppointmentInjuryDetail>(
            db, sourceAppointmentId, targetAppointmentId, tenantId,
            onCopied: (source, cloneId) => injuryIdMap[source.Id] = cloneId);

        var bodyParts = await CopyBodyPartsAsync(db, injuryIdMap, tenantId);

        return new CopiedGroupCounts(
            Accessors: accessors,
            ApplicantAttorneys: applicantAttorneys,
            DefenseAttorneys: defenseAttorneys,
            ClaimExaminers: claimExaminers,
            EmployerDetails: employerDetails,
            InjuryDetails: injuryDetails,
            BodyParts: bodyParts,
            PrimaryInsurances: primaryInsurances,
            CustomFieldValues: customFieldValues,
            Documents: documents);
    }

    /// <summary>
    /// Copies every row of a group whose FK is <c>AppointmentId</c>. The FK is read and written
    /// through EF metadata rather than a typed property so one method serves all nine groups.
    /// </summary>
    private async Task<int> CopyDirectChildrenAsync<TEntity>(
        CaseEvaluationDbContext db,
        Guid sourceAppointmentId,
        Guid targetAppointmentId,
        Guid? tenantId,
        Action<TEntity, Guid>? onCopied = null)
        where TEntity : class
    {
        var sources = await db.Set<TEntity>()
            .AsNoTracking()
            .Where(e => EF.Property<Guid>(e, nameof(Appointment) + "Id") == sourceAppointmentId)
            .ToListAsync();

        foreach (var source in sources)
        {
            var cloneId = _guidGenerator.Create();
            var clone = CloneScalars(db, source, cloneId, tenantId);
            db.Entry(clone).Property(nameof(Appointment) + "Id").CurrentValue = targetAppointmentId;
            db.Set<TEntity>().Add(clone);
            onCopied?.Invoke(source, cloneId);
        }

        return sources.Count;
    }

    /// <summary>
    /// Body parts are a GRANDCHILD: they carry no <c>AppointmentId</c>, only
    /// <see cref="AppointmentBodyPart.AppointmentInjuryDetailId"/>. Each is re-pointed at the NEW
    /// injury detail. If this method is skipped, nothing else fails -- which is precisely how a
    /// cascade copier drops a group without anyone noticing.
    /// </summary>
    private async Task<int> CopyBodyPartsAsync(
        CaseEvaluationDbContext db,
        IReadOnlyDictionary<Guid, Guid> injuryIdMap,
        Guid? tenantId)
    {
        if (injuryIdMap.Count == 0)
        {
            return 0;
        }

        var sourceInjuryIds = injuryIdMap.Keys.ToList();
        var sources = await db.Set<AppointmentBodyPart>()
            .AsNoTracking()
            .Where(bp => sourceInjuryIds.Contains(bp.AppointmentInjuryDetailId))
            .ToListAsync();

        foreach (var source in sources)
        {
            var clone = CloneScalars(db, source, _guidGenerator.Create(), tenantId);
            clone.AppointmentInjuryDetailId = injuryIdMap[source.AppointmentInjuryDetailId];
            db.Set<AppointmentBodyPart>().Add(clone);
        }

        return sources.Count;
    }

    /// <summary>
    /// Clones every mapped scalar of <paramref name="source"/> onto a fresh instance, then stamps a
    /// new identity and clears the audit / concurrency columns so ABP writes them afresh on insert.
    /// Properties are cleared only when the entity actually maps them, so this serves entities with
    /// and without full auditing.
    /// </summary>
    private static TEntity CloneScalars<TEntity>(
        CaseEvaluationDbContext db,
        TEntity source,
        Guid newId,
        Guid? tenantId)
        where TEntity : class
    {
        var clone = (TEntity)Activator.CreateInstance(typeof(TEntity), nonPublic: true)!;
        var entry = db.Entry(clone);
        entry.CurrentValues.SetValues(source);

        SetIfMapped(entry, "Id", newId);
        SetIfMapped(entry, "TenantId", tenantId);

        // A copy is a NEW row: it must not inherit who created the original or when, and an
        // inherited ConcurrencyStamp would collide with the source's optimistic-concurrency token.
        SetIfMapped(entry, "CreationTime", default(DateTime));
        SetIfMapped(entry, "CreatorId", null);
        SetIfMapped(entry, "LastModificationTime", null);
        SetIfMapped(entry, "LastModifierId", null);
        SetIfMapped(entry, "IsDeleted", false);
        SetIfMapped(entry, "DeleterId", null);
        SetIfMapped(entry, "DeletionTime", null);
        SetIfMapped(entry, "ConcurrencyStamp", Guid.NewGuid().ToString("N"));

        return clone;
    }

    private static void SetIfMapped(EntityEntry entry, string propertyName, object? value)
    {
        if (entry.Metadata.FindProperty(propertyName) != null)
        {
            entry.Property(propertyName).CurrentValue = value;
        }
    }
}
