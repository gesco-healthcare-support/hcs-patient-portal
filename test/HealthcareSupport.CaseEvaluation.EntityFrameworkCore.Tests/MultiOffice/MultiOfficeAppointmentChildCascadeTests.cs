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
using HealthcareSupport.CaseEvaluation.Enums;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// Phase 4d (2026-08-05) -- one test per child group for
/// <see cref="IAppointmentChildCascadeCopier"/>.
///
/// <para>WHY ONE TEST PER GROUP RATHER THAN ONE TEST FOR THE COPIER. Bug F18 was a cascade copier
/// that silently dropped 2 of 8 groups. A single "everything copied" assertion hides exactly that:
/// it fails on the first missing group and tells you nothing about the rest. One test per group
/// means deleting any single group from the copier fails EXACTLY ONE test, which is the property
/// the mutation checks verify.</para>
///
/// <para>Each test asserts FULL FIELD EQUALITY, not a row count. A copier that creates the right
/// number of rows with blank fields would pass a count assertion -- the field-level version of the
/// same bug. The comparison walks the EF model metadata, so a column added to a child entity later
/// is compared automatically without touching this file.</para>
/// </summary>
[Collection(MultiOfficeCollection.Name)]
public class MultiOfficeAppointmentChildCascadeTests : CaseEvaluationMultiOfficeTestBase
{
    /// <summary>Never compared: identity, the re-pointed FKs, and the audit columns of a new row.</summary>
    private static readonly HashSet<string> NotCompared = new(StringComparer.Ordinal)
    {
        "Id",
        "AppointmentId",
        "AppointmentInjuryDetailId",
        "CreationTime",
        "CreatorId",
        "LastModificationTime",
        "LastModifierId",
        "IsDeleted",
        "DeleterId",
        "DeletionTime",
        "ConcurrencyStamp",
        "ExtraProperties",
    };

    private readonly IAppointmentChildCascadeCopier _copier;
    private readonly IDbContextProvider<CaseEvaluationDbContext> _dbContextProvider;
    private readonly ICurrentTenant _currentTenant;

    public MultiOfficeAppointmentChildCascadeTests()
    {
        _copier = GetRequiredService<IAppointmentChildCascadeCopier>();
        _dbContextProvider = GetRequiredService<IDbContextProvider<CaseEvaluationDbContext>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    // ---------------- one test per group ----------------

    [Fact]
    public async Task Copies_accessors()
    {
        await RunCopyAsync(async ctx =>
        {
            var copied = await LoadAsync<AppointmentAccessor>(ctx.TargetId);
            var source = await LoadAsync<AppointmentAccessor>(ctx.SourceId);

            copied.Count.ShouldBe(1);
            AssertFieldsMatch(source, copied, a => a.IdentityUserId);
        });
    }

    [Fact]
    public async Task Copies_applicant_attorneys()
    {
        await RunCopyAsync(async ctx =>
        {
            var copied = await LoadAsync<AppointmentApplicantAttorney>(ctx.TargetId);
            var source = await LoadAsync<AppointmentApplicantAttorney>(ctx.SourceId);

            copied.Count.ShouldBe(1);
            AssertFieldsMatch(source, copied, a => a.ApplicantAttorneyId);
        });
    }

    [Fact]
    public async Task Copies_defense_attorneys()
    {
        await RunCopyAsync(async ctx =>
        {
            var copied = await LoadAsync<AppointmentDefenseAttorney>(ctx.TargetId);
            var source = await LoadAsync<AppointmentDefenseAttorney>(ctx.SourceId);

            copied.Count.ShouldBe(1);
            AssertFieldsMatch(source, copied, a => a.DefenseAttorneyId);
        });
    }

    [Fact]
    public async Task Copies_claim_examiners()
    {
        await RunCopyAsync(async ctx =>
        {
            var copied = await LoadAsync<AppointmentClaimExaminer>(ctx.TargetId);
            var source = await LoadAsync<AppointmentClaimExaminer>(ctx.SourceId);

            copied.Count.ShouldBe(1);
            AssertFieldsMatch(source, copied, c => c.Name);
        });
    }

    [Fact]
    public async Task Copies_employer_details()
    {
        await RunCopyAsync(async ctx =>
        {
            var copied = await LoadAsync<AppointmentEmployerDetail>(ctx.TargetId);
            var source = await LoadAsync<AppointmentEmployerDetail>(ctx.SourceId);

            copied.Count.ShouldBe(1);
            AssertFieldsMatch(source, copied, e => e.EmployerName);
        });
    }

    [Fact]
    public async Task Copies_injury_details()
    {
        await RunCopyAsync(async ctx =>
        {
            var copied = await LoadAsync<AppointmentInjuryDetail>(ctx.TargetId);
            var source = await LoadAsync<AppointmentInjuryDetail>(ctx.SourceId);

            copied.Count.ShouldBe(2);
            AssertFieldsMatch(source, copied, i => i.ClaimNumber);
        });
    }

    [Fact]
    public async Task Copies_primary_insurances()
    {
        await RunCopyAsync(async ctx =>
        {
            var copied = await LoadAsync<AppointmentPrimaryInsurance>(ctx.TargetId);
            var source = await LoadAsync<AppointmentPrimaryInsurance>(ctx.SourceId);

            copied.Count.ShouldBe(1);
            AssertFieldsMatch(source, copied, p => p.Name);
        });
    }

    [Fact]
    public async Task Copies_custom_field_values()
    {
        await RunCopyAsync(async ctx =>
        {
            var copied = await LoadAsync<CustomFieldValue>(ctx.TargetId);
            var source = await LoadAsync<CustomFieldValue>(ctx.SourceId);

            copied.Count.ShouldBe(1);
            AssertFieldsMatch(source, copied, v => v.Value);
        });
    }

    [Fact]
    public async Task Copies_documents_and_shares_the_blob()
    {
        await RunCopyAsync(async ctx =>
        {
            var copied = await LoadAsync<AppointmentDocument>(ctx.TargetId);
            var source = await LoadAsync<AppointmentDocument>(ctx.SourceId);

            copied.Count.ShouldBe(1);
            AssertFieldsMatch(source, copied, d => d.FileName);

            // The blob itself is never duplicated: the copied row points at the SAME object key.
            copied[0].BlobName.ShouldBe(source[0].BlobName);
        });
    }

    /// <summary>
    /// Body parts are the group a naive copier drops: they carry no <c>AppointmentId</c>, so
    /// filtering every table by appointment misses them entirely. They must also be RE-POINTED at
    /// the newly copied injury details rather than left aimed at the source's.
    /// </summary>
    [Fact]
    public async Task Copies_body_parts_and_repoints_them_at_the_new_injury_details()
    {
        await RunCopyAsync(async ctx =>
        {
            var db = await _dbContextProvider.GetDbContextAsync();

            var sourceInjuryIds = (await LoadAsync<AppointmentInjuryDetail>(ctx.SourceId)).Select(i => i.Id).ToList();
            var targetInjuryIds = (await LoadAsync<AppointmentInjuryDetail>(ctx.TargetId)).Select(i => i.Id).ToList();

            var copiedParts = await db.Set<AppointmentBodyPart>().AsNoTracking()
                .Where(bp => targetInjuryIds.Contains(bp.AppointmentInjuryDetailId))
                .ToListAsync();
            var sourceParts = await db.Set<AppointmentBodyPart>().AsNoTracking()
                .Where(bp => sourceInjuryIds.Contains(bp.AppointmentInjuryDetailId))
                .ToListAsync();

            // Two injury details, two body parts each.
            sourceParts.Count.ShouldBe(4);
            copiedParts.Count.ShouldBe(4);

            copiedParts.Select(p => p.BodyPartDescription).OrderBy(d => d)
                .ShouldBe(sourceParts.Select(p => p.BodyPartDescription).OrderBy(d => d));

            // Not one copied part may still hang off a SOURCE injury detail.
            copiedParts.ShouldAllBe(p => !sourceInjuryIds.Contains(p.AppointmentInjuryDetailId));
        });
    }

    // ---------------- what must NOT be copied ----------------

    [Fact]
    public async Task Does_not_copy_change_requests_packets_or_info_requests()
    {
        await RunCopyAsync(async ctx =>
        {
            var db = await _dbContextProvider.GetDbContextAsync();

            // The change request stays with the appointment it was filed against -- repointing it
            // would falsify the record consent was agreed on.
            (await db.Set<AppointmentChangeRequests.AppointmentChangeRequest>().AsNoTracking()
                .CountAsync(c => c.AppointmentId == ctx.TargetId)).ShouldBe(0);

            // Packets are REGENERATED for the new appointment, never copied: the unique index is
            // (TenantId, AppointmentId, Kind) and packet content embeds the appointment date.
            (await db.Set<AppointmentPacket>().AsNoTracking()
                .CountAsync(p => p.AppointmentId == ctx.TargetId)).ShouldBe(0);
        });
    }

    [Fact]
    public async Task Reports_a_count_for_every_group()
    {
        await RunCopyAsync(ctx =>
        {
            // The per-group result is what makes a dropped group visible as a zero rather than
            // hiding inside a total.
            ctx.Counts.Accessors.ShouldBe(1);
            ctx.Counts.ApplicantAttorneys.ShouldBe(1);
            ctx.Counts.DefenseAttorneys.ShouldBe(1);
            ctx.Counts.ClaimExaminers.ShouldBe(1);
            ctx.Counts.EmployerDetails.ShouldBe(1);
            ctx.Counts.InjuryDetails.ShouldBe(2);
            ctx.Counts.BodyParts.ShouldBe(4);
            ctx.Counts.PrimaryInsurances.ShouldBe(1);
            ctx.Counts.CustomFieldValues.ShouldBe(1);
            ctx.Counts.Documents.ShouldBe(1);
            return Task.CompletedTask;
        });
    }

    // ---------------- harness ----------------

    private sealed record CopyContext(Guid SourceId, Guid TargetId, CopiedGroupCounts Counts);

    /// <summary>
    /// Seeds a source appointment with a row in every group, an empty target appointment, runs the
    /// copier, then hands both ids to the assertion. Each test gets its OWN pair so one test's
    /// copy cannot be mistaken for another's.
    /// </summary>
    private async Task RunCopyAsync(Func<CopyContext, Task> assert)
    {
        var (officeA, _) = await GetSeededOfficesAsync();
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        CopiedGroupCounts? counts = null;

        await InOfficeAsync(officeA, async () =>
        {
            await SeedAppointmentAsync(officeA, sourceId, "SRC");
            await SeedAppointmentAsync(officeA, targetId, "TGT");
            await SeedChildrenAsync(officeA, sourceId);
        });

        await InOfficeAsync(officeA, async () =>
        {
            counts = await _copier.CopyAllAsync(sourceId, targetId, officeA.OfficeId);
            var db = await _dbContextProvider.GetDbContextAsync();
            await db.SaveChangesAsync();
        });

        await InOfficeAsync(officeA, () => assert(new CopyContext(sourceId, targetId, counts!)));
    }

    private async Task SeedAppointmentAsync(SeededOffice office, Guid appointmentId, string label)
    {
        var repository = GetRequiredService<IRepository<Appointment, Guid>>();
        await repository.InsertAsync(new Appointment(
            id: appointmentId,
            patientId: office.PatientId,
            identityUserId: null,
            appointmentTypeId: office.AppointmentTypeId,
            locationId: office.LocationId,
            doctorAvailabilityId: office.DoctorAvailabilityId,
            appointmentDate: DateTime.Today.AddDays(30),
            requestConfirmationNumber: $"RCN-CASCADE-{label}-{appointmentId:N}".Substring(0, 20),
            appointmentStatus: AppointmentStatusType.Approved), autoSave: true);
    }

    private async Task SeedChildrenAsync(SeededOffice office, Guid appointmentId)
    {
        var db = await _dbContextProvider.GetDbContextAsync();

        // These groups carry REQUIRED FKs to rows the office seeder does not create, so the parents
        // are seeded here first. Without them SQLite rejects the insert with a bare
        // "FOREIGN KEY constraint failed" that names no column.
        var applicantAttorneyId = Guid.NewGuid();
        var defenseAttorneyId = Guid.NewGuid();
        var customFieldId = Guid.NewGuid();
        db.Set<ApplicantAttorneys.ApplicantAttorney>().Add(
            new ApplicantAttorneys.ApplicantAttorney(applicantAttorneyId, office.StateId, null, firmName: "Synthetic AA Firm"));
        db.Set<DefenseAttorneys.DefenseAttorney>().Add(
            new DefenseAttorneys.DefenseAttorney(defenseAttorneyId, office.StateId, null, firmName: "Synthetic DA Firm"));
        db.Set<CustomField>().Add(new CustomField(
            id: customFieldId,
            tenantId: office.OfficeId,
            fieldLabel: "Synthetic field",
            displayOrder: 1,
            fieldType: CustomFieldType.Alphanumeric,
            appointmentTypeId: office.AppointmentTypeId));
        await db.SaveChangesAsync();

        // Only ONE accessor: AppointmentAccessor.IdentityUserId is a REQUIRED FK to IdentityUser,
        // and the office seeder creates exactly one real user (the booker).
        db.Set<AppointmentAccessor>().Add(new AppointmentAccessor(Guid.NewGuid(), office.BookerUserId, appointmentId, AccessType.Edit));

        db.Set<AppointmentApplicantAttorney>().Add(new AppointmentApplicantAttorney(Guid.NewGuid(), appointmentId, applicantAttorneyId, null));
        db.Set<AppointmentDefenseAttorney>().Add(new AppointmentDefenseAttorney(Guid.NewGuid(), appointmentId, defenseAttorneyId, null));

        var examiner = new AppointmentClaimExaminer(Guid.NewGuid(), appointmentId, isActive: true) { Name = "Synthetic Examiner", Email = "ce@example.test" };
        db.Set<AppointmentClaimExaminer>().Add(examiner);

        var employer = new AppointmentEmployerDetail(Guid.NewGuid(), appointmentId, office.StateId, "Synthetic Employer", "Occupation") { City = "Testville" };
        db.Set<AppointmentEmployerDetail>().Add(employer);

        var insurance = new AppointmentPrimaryInsurance(Guid.NewGuid(), appointmentId, isActive: true) { Name = "Synthetic Insurer" };
        db.Set<AppointmentPrimaryInsurance>().Add(insurance);

        db.Set<CustomFieldValue>().Add(new CustomFieldValue(
            id: Guid.NewGuid(),
            tenantId: office.OfficeId,
            customFieldId: customFieldId,
            appointmentId: appointmentId,
            value: "Synthetic value"));

        for (var i = 1; i <= 2; i++)
        {
            var injuryId = Guid.NewGuid();
            db.Set<AppointmentInjuryDetail>().Add(BuildInjuryDetail(injuryId, appointmentId, i));
            db.Set<AppointmentBodyPart>().Add(new AppointmentBodyPart(Guid.NewGuid(), injuryId, $"Body part {i}A"));
            db.Set<AppointmentBodyPart>().Add(new AppointmentBodyPart(Guid.NewGuid(), injuryId, $"Body part {i}B"));
        }

        db.Set<AppointmentDocument>().Add(new AppointmentDocument(
            id: Guid.NewGuid(),
            tenantId: office.OfficeId,
            appointmentId: appointmentId,
            documentName: "Synthetic doc",
            fileName: "synthetic.pdf",
            blobName: "blob/synthetic.pdf",
            contentType: "application/pdf",
            fileSize: 1024,
            uploadedByUserId: office.BookerUserId));

        await db.SaveChangesAsync();
    }

    private static AppointmentInjuryDetail BuildInjuryDetail(Guid id, Guid appointmentId, int index)
    {
        var ctor = typeof(AppointmentInjuryDetail).GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .First();
        var args = ctor.GetParameters().Select(p => (object?)(p.Name switch
        {
            "id" => id,
            "appointmentId" => appointmentId,
            "claimNumber" => $"CLM-{index}",
            "wcabAdj" => $"ADJ-{index}",
            "bodyPartsSummary" => $"Summary {index}",
            _ => DefaultFor(p.ParameterType),
        })).ToArray();
        return (AppointmentInjuryDetail)ctor.Invoke(args);
    }

    private static object? DefaultFor(Type t) =>
        t == typeof(string) ? "x" : (t.IsValueType ? Activator.CreateInstance(t) : null);

    private async Task<List<TEntity>> LoadAsync<TEntity>(Guid appointmentId) where TEntity : class
    {
        var db = await _dbContextProvider.GetDbContextAsync();
        return await db.Set<TEntity>().AsNoTracking()
            .Where(e => EF.Property<Guid>(e, "AppointmentId") == appointmentId)
            .ToListAsync();
    }

    /// <summary>
    /// Compares every mapped scalar of each copied row against its source, excluding identity, the
    /// re-pointed FKs and audit columns. Driven by EF model metadata so a NEW column on a child
    /// entity is compared automatically -- the guard against a copier that quietly stops carrying
    /// a field.
    /// </summary>
    private void AssertFieldsMatch<TEntity>(
        IReadOnlyList<TEntity> source,
        IReadOnlyList<TEntity> copied,
        Func<TEntity, object?> orderBy)
        where TEntity : class
    {
        copied.Count.ShouldBe(source.Count, $"{typeof(TEntity).Name}: row count");

        var db = _dbContextProvider.GetDbContextAsync().GetAwaiter().GetResult();
        var properties = db.Model.FindEntityType(typeof(TEntity))!
            .GetProperties()
            .Where(p => !NotCompared.Contains(p.Name))
            .ToList();

        properties.ShouldNotBeEmpty($"{typeof(TEntity).Name}: nothing left to compare");

        var orderedSource = source.OrderBy(orderBy).ToList();
        var orderedCopied = copied.OrderBy(orderBy).ToList();

        for (var i = 0; i < orderedSource.Count; i++)
        {
            foreach (var property in properties)
            {
                var expected = db.Entry(orderedSource[i]).Property(property.Name).CurrentValue;
                var actual = db.Entry(orderedCopied[i]).Property(property.Name).CurrentValue;
                actual.ShouldBe(expected, $"{typeof(TEntity).Name}.{property.Name} did not copy");
            }
        }
    }

    private Task InOfficeAsync(SeededOffice office, Func<Task> body) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(office.OfficeId))
            {
                await body();
            }
        }, requiresNew: true);
}
