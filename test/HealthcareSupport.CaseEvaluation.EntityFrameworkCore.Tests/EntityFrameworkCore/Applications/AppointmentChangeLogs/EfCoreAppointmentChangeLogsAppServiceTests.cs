using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentBodyParts;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.TestData;
using NSubstitute;
using Shouldly;
using Volo.Abp.Auditing;
using Volo.Abp.AuditLogging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeLogs;

/// <summary>
/// Group K. Integration coverage for <see cref="AppointmentChangeLogsAppService"/>, the read-only
/// change-log over ABP audit data.
///
/// <para><b>THE SEAM.</b> The five intake repositories are the REAL EF Core ones from DI, running
/// against the SQLite rig, because "which rows belong to this appointment" is the rule most of
/// this service exists to express and a substituted repository would just replay the answer the
/// test already assumed. <see cref="IAuditLogRepository"/> is the ONE substitute: ABP writes
/// <c>EntityChange</c> rows from its own auditing middleware, which this rig does not run, so a
/// real audit repository returns nothing at all and every assertion below would be vacuous. The
/// substitute both supplies rows and RECORDS the query shape the service asked for, which is the
/// only observable for the scan-set rules.</para>
///
/// <para><b>WHY THE SERVICE IS CONSTRUCTED BY HAND</b> rather than resolved. Resolving it from DI
/// binds it to the real <see cref="IAuditLogRepository"/> and the substitute can never be injected.
/// Manual construction skips ABP's interceptors, so <c>LazyServiceProvider</c> is assigned
/// explicitly - that is what makes the inherited <c>AsyncExecuter</c> resolve. Same idiom as
/// <c>AppointmentAccessorManagerTests.BuildManager</c>.</para>
///
/// <para><b>WHAT THESE TESTS DO NOT PIN.</b>
/// (1) Authorization. The class carries
/// <c>[Authorize(CaseEvaluationPermissions.AppointmentChangeLogs.Default)]</c>, but the rig calls
/// <c>AddAlwaysAllowAuthorization()</c> AND manual construction skips the interceptor, so an
/// authorization assertion here has no failing input and none is written.
/// (2) DTO / FluentValidation. Those run in the interceptor this shape bypasses, so
/// <c>MaxResultCount</c> range checks are not this service's behaviour and are not asserted.
/// (3) <c>input.Sorting</c>. It is inherited from <c>PagedAndSortedResultRequestDto</c> and
/// therefore appears on the API contract, but the service ignores it and always orders by
/// <c>ChangeTime</c> descending. Pinning "it is ignored" would pin a defect as a guarantee.
/// (4) Any real <c>IAuditLogRepository</c> query translation, and the tenant scoping of audit
/// rows - <c>ToRaw</c> never reads <c>EntityChange.EntityTenantId</c>, so isolation of audit data
/// rests entirely on the repository, which is substituted here.</para>
///
/// <para><b>TWO RIG TRAPS THAT DO NOT APPLY HERE, checked rather than assumed.</b> This service
/// publishes no events and dispatches no notifications - there is no <c>IEventBus</c>, no ETO and
/// no <c>INotificationDispatcher</c> in the file - so neither the "local events dispatch at
/// unit-of-work completion" trap nor the notification-template wall can bite. Results are still
/// hoisted out of the <c>WithUnitOfWorkAsync</c> lambda and asserted after it returns, so the
/// shape stays correct if the service ever starts publishing.</para>
///
/// <para><b>THE TRAP THAT DOES APPLY.</b> All five intake entities are <c>IMultiTenant</c> and
/// Appointment1 lives in TenantA, so every child query returns EMPTY unless the call runs inside
/// <c>ICurrentTenant.Change(TenantARef)</c>. Without it the scan-set tests would pass with the
/// whole child-resolution block deleted.</para>
///
/// <para><b>NO ASSERTION ON A COUNT OF ROWS.</b> The rig is shared and accumulates across the
/// whole run. Counts below are only ever over DTOs built from <c>EntityChange</c> objects this
/// file's own substitute returned, or over calls this file's own service instance made to it.
/// Every seeded identifier carries a per-test token.</para>
/// </summary>
[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreAppointmentChangeLogsAppServiceTests
    : CaseEvaluationApplicationTestBase<CaseEvaluationEntityFrameworkCoreTestModule>
{
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<AppointmentInjuryDetail, Guid> _injuryRepository;
    private readonly IRepository<AppointmentBodyPart, Guid> _bodyPartRepository;
    private readonly IRepository<AppointmentClaimExaminer, Guid> _claimExaminerRepository;
    private readonly IRepository<AppointmentPrimaryInsurance, Guid> _primaryInsuranceRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IAbpLazyServiceProvider _lazyServiceProvider;

    public EfCoreAppointmentChangeLogsAppServiceTests()
    {
        _appointmentRepository = GetRequiredService<IRepository<Appointment, Guid>>();
        _injuryRepository = GetRequiredService<IRepository<AppointmentInjuryDetail, Guid>>();
        _bodyPartRepository = GetRequiredService<IRepository<AppointmentBodyPart, Guid>>();
        _claimExaminerRepository = GetRequiredService<IRepository<AppointmentClaimExaminer, Guid>>();
        _primaryInsuranceRepository = GetRequiredService<IRepository<AppointmentPrimaryInsurance, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
        _lazyServiceProvider = GetRequiredService<IAbpLazyServiceProvider>();
    }

    // ---------------------------------------------------------------------------------------
    // GetByAppointmentAsync
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// The scan set is the appointment PLUS its own injury details, the body parts of THOSE
    /// injuries, its own claim examiners and its own primary insurances - and nothing belonging
    /// to another appointment.
    /// </summary>
    [Fact]
    public async Task GetByAppointmentAsync_scans_the_appointment_and_only_its_own_child_entities()
    {
        var token = NewToken();
        var own = IntakeChildIds.New();
        var decoy = IntakeChildIds.New();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await SeedIntakeChildrenAsync(AppointmentsTestData.Appointment1Id, own, token + "own");

                // LOAD-BEARING, NOT SETUP NOISE. A negative guarantee needs the thing it is meant
                // to exclude to be PRESENT and VISIBLE. These rows hang off a different
                // appointment but are written INSIDE TenantA, so the multi-tenancy filter cannot
                // be what removes them - only the service's own `x.AppointmentId == appointmentId`
                // predicates can. Seeded under TenantB they would be filtered out anyway and this
                // test would stay green with every one of those predicates deleted.
                await SeedIntakeChildrenAsync(AppointmentsTestData.Appointment2Id, decoy, token + "dec");
            }
        });

        var (auditLog, scans) = StubAuditLog();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await BuildService(auditLog).GetByAppointmentAsync(AppointmentsTestData.Appointment1Id);
            }
        });

        var appointmentId = AppointmentsTestData.Appointment1Id.ToString();
        scans.ShouldContain(s => s.EntityId == appointmentId
            && s.EntityTypeFullName == AppointmentAuditedEntities.Appointment);
        scans.ShouldContain(s => s.EntityId == own.InjuryId.ToString()
            && s.EntityTypeFullName == AppointmentAuditedEntities.InjuryDetail);
        scans.ShouldContain(s => s.EntityId == own.BodyPartId.ToString()
            && s.EntityTypeFullName == AppointmentAuditedEntities.BodyPart);
        scans.ShouldContain(s => s.EntityId == own.ExaminerId.ToString()
            && s.EntityTypeFullName == AppointmentAuditedEntities.ClaimExaminer);
        scans.ShouldContain(s => s.EntityId == own.InsuranceId.ToString()
            && s.EntityTypeFullName == AppointmentAuditedEntities.PrimaryInsurance);

        scans.ShouldNotContain(s => s.EntityId == decoy.InjuryId.ToString());
        scans.ShouldNotContain(s => s.EntityId == decoy.BodyPartId.ToString());
        scans.ShouldNotContain(s => s.EntityId == decoy.ExaminerId.ToString());
        scans.ShouldNotContain(s => s.EntityId == decoy.InsuranceId.ToString());
    }

    /// <summary>
    /// Every per-entity audit query asks for details and caps the scan.
    /// </summary>
    [Fact]
    public async Task GetByAppointmentAsync_requests_details_and_caps_the_scan()
    {
        // A fresh id no row references, so exactly one scan happens and the accumulating rig
        // cannot add unrelated ones.
        var appointmentId = Guid.NewGuid();
        var (auditLog, scans) = StubAuditLog();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await BuildService(auditLog).GetByAppointmentAsync(appointmentId);
            }
        });

        scans.ShouldNotBeEmpty();

        // includeDetails is behaviour, not decoration: with it false ABP returns EntityChange rows
        // whose PropertyChanges collection is empty, the builder emits no rows at all, and the
        // whole view silently goes blank.
        scans.ShouldAllBe(s => s.IncludeDetails);

        // COVERAGE-ORIENTED, and labelled as such: this pins the ScanCap constant's value, not a
        // rule. The only mutation it catches is a change to `private const int ScanCap = 1000`.
        scans.ShouldAllBe(s => s.MaxResultCount == 1000);
    }

    /// <summary>
    /// Rows come back newest first.
    /// </summary>
    [Fact]
    public async Task GetByAppointmentAsync_returns_the_newest_change_first()
    {
        var appointmentId = Guid.NewGuid();
        var older = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        var newer = new DateTime(2026, 6, 8, 8, 0, 0, DateTimeKind.Utc);

        // Supplied OLDEST first, so an unordered pass-through returns them the wrong way round.
        // PanelNumber is on the AuditFieldPolicy allowlist, so both rows survive redaction.
        var (auditLog, _) = StubAuditLog(scan => scan.EntityTypeFullName == AppointmentAuditedEntities.Appointment
            ? new List<EntityChange>
            {
                Change(AppointmentAuditedEntities.Appointment, appointmentId.ToString(),
                    EntityChangeType.Updated, older, ("PanelNumber", "TEST-P1", "TEST-P2")),
                Change(AppointmentAuditedEntities.Appointment, appointmentId.ToString(),
                    EntityChangeType.Updated, newer, ("PanelNumber", "TEST-P2", "TEST-P3")),
            }
            : new List<EntityChange>());

        var rows = await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                return await BuildService(auditLog).GetByAppointmentAsync(appointmentId);
            }
        });

        rows.Count.ShouldBe(2);
        rows[0].ChangeTime.ShouldBe(newer);
        rows[1].ChangeTime.ShouldBe(older);
    }

    // ---------------------------------------------------------------------------------------
    // GetListAsync - resolving which appointment (if any) the list is about
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// An explicit appointment id short-circuits before the confirmation-number lookup.
    /// </summary>
    [Fact]
    public async Task GetListAsync_appointment_id_wins_over_confirmation_number()
    {
        // The confirmation number IS resolvable in TenantA - proven independently by
        // GetListAsync_resolves_the_appointment_from_its_confirmation_number, which is the control
        // for this test. So if precedence were reversed, the scan would target Appointment1.
        var unknownAppointmentId = Guid.NewGuid();
        var input = new GetAppointmentChangeLogsInput
        {
            AppointmentId = unknownAppointmentId,
            RequestConfirmationNumber = AppointmentsTestData.Appointment1RequestConfirmationNumber,
        };

        var scans = await ListAsync(input);

        scans.ShouldContain(s => s.EntityId == unknownAppointmentId.ToString());
        scans.ShouldNotContain(s => s.EntityId == AppointmentsTestData.Appointment1Id.ToString());
    }

    /// <summary>
    /// A confirmation number with no id routes to the per-appointment scan, not the global one.
    /// </summary>
    [Fact]
    public async Task GetListAsync_resolves_the_appointment_from_its_confirmation_number()
    {
        var input = new GetAppointmentChangeLogsInput
        {
            RequestConfirmationNumber = AppointmentsTestData.Appointment1RequestConfirmationNumber,
        };

        var scans = await ListAsync(input);

        scans.ShouldContain(s => s.EntityId == AppointmentsTestData.Appointment1Id.ToString());

        // A null entityId is the global branch's signature. Asserting its absence is what stops
        // this passing if the resolution is deleted and the call falls through to the cross-type
        // scan, which would still have produced an Appointment-typed query.
        scans.ShouldNotContain(s => s.EntityId == null);
    }

    /// <summary>
    /// An unmatched confirmation number falls back to the global cross-type scan; it does not
    /// short-circuit to an empty page.
    /// </summary>
    [Fact]
    public async Task GetListAsync_unknown_confirmation_number_falls_back_to_the_global_scan()
    {
        var input = new GetAppointmentChangeLogsInput
        {
            RequestConfirmationNumber = "TEST-NO-SUCH-RCN-" + NewToken(),
        };

        var scans = await ListAsync(input);

        // Asserted against the five type CONSTANTS, deliberately not by iterating
        // AppointmentAuditedEntities.All: iterating All would shrink in step with a type removed
        // from it, and the test would stay green on exactly the mutation it should catch.
        scans.ShouldContain(s => s.EntityId == null
            && s.EntityTypeFullName == AppointmentAuditedEntities.Appointment);
        scans.ShouldContain(s => s.EntityId == null
            && s.EntityTypeFullName == AppointmentAuditedEntities.InjuryDetail);
        scans.ShouldContain(s => s.EntityId == null
            && s.EntityTypeFullName == AppointmentAuditedEntities.BodyPart);
        scans.ShouldContain(s => s.EntityId == null
            && s.EntityTypeFullName == AppointmentAuditedEntities.ClaimExaminer);
        scans.ShouldContain(s => s.EntityId == null
            && s.EntityTypeFullName == AppointmentAuditedEntities.PrimaryInsurance);
    }

    /// <summary>
    /// The global scan pushes the time window and the parsed change type down into the audit
    /// query, and the parse is case-insensitive.
    /// </summary>
    [Fact]
    public async Task GetListAsync_global_scan_forwards_the_window_and_the_parsed_change_type()
    {
        var start = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var input = new GetAppointmentChangeLogsInput
        {
            // Lower case on purpose: this is the only route to ParseChangeType, and dropping its
            // ignoreCase flag makes Enum.TryParse fail and forward null instead.
            ChangeType = "updated",
            StartTime = start,
            EndTime = end,
        };

        var scans = await ListAsync(input);

        scans.ShouldNotBeEmpty();
        scans.ShouldAllBe(s => s.StartTime == start);
        scans.ShouldAllBe(s => s.EndTime == end);
        scans.ShouldAllBe(s => s.ChangeType == EntityChangeType.Updated);
    }

    // ---------------------------------------------------------------------------------------
    // GetListAsync - in-memory filtering, paging and redaction
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// An unparseable change type forwards no server-side filter, yet the in-memory filter still
    /// rejects every row.
    /// </summary>
    [Fact]
    public async Task GetListAsync_unparseable_change_type_yields_an_empty_page()
    {
        var appointmentId = Guid.NewGuid();
        var input = new GetAppointmentChangeLogsInput { ChangeType = "TEST-BOGUS-CHANGE-TYPE" };

        // The fixture is deliberately NON-empty: one row that would survive with the ChangeType
        // clause of ApplyFilters deleted. Against an empty fixture the assertion below would be
        // satisfied by the absence of data rather than by the filter.
        var (auditLog, scans) = StubAuditLog(scan =>
            scan.EntityTypeFullName == AppointmentAuditedEntities.Appointment
                ? new List<EntityChange>
                {
                    Change(AppointmentAuditedEntities.Appointment, appointmentId.ToString(),
                        EntityChangeType.Updated, new DateTime(2026, 6, 8, 8, 0, 0, DateTimeKind.Utc),
                        ("PanelNumber", "TEST-P1", "TEST-P2")),
                }
                : new List<EntityChange>());

        var result = await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                return await BuildService(auditLog).GetListAsync(input);
            }
        });

        scans.ShouldNotBeEmpty();
        scans.ShouldAllBe(s => s.ChangeType == null);
        result.TotalCount.ShouldBe(0);
        result.Items.ShouldBeEmpty();
    }

    /// <summary>
    /// The entity-type filter matches the friendly label, ignoring case - not the fully qualified
    /// type name the audit rows carry.
    /// </summary>
    [Fact]
    public async Task GetListAsync_filters_by_the_friendly_entity_label()
    {
        var appointmentId = Guid.NewGuid();
        var injuryId = Guid.NewGuid();
        var when = new DateTime(2026, 6, 8, 8, 0, 0, DateTimeKind.Utc);
        var input = new GetAppointmentChangeLogsInput { EntityType = "injury detail" };

        var (auditLog, _) = StubAuditLog(scan =>
        {
            if (scan.EntityTypeFullName == AppointmentAuditedEntities.Appointment)
            {
                return new List<EntityChange>
                {
                    Change(AppointmentAuditedEntities.Appointment, appointmentId.ToString(),
                        EntityChangeType.Updated, when, ("PanelNumber", "TEST-P1", "TEST-P2")),
                };
            }

            if (scan.EntityTypeFullName == AppointmentAuditedEntities.InjuryDetail)
            {
                // DateOfInjury is the one allowlisted injury-detail field, so the row survives
                // redaction and can be identified by name below.
                return new List<EntityChange>
                {
                    Change(AppointmentAuditedEntities.InjuryDetail, injuryId.ToString(),
                        EntityChangeType.Updated, when, ("DateOfInjury", "2026-01-15", "2026-01-16")),
                };
            }

            return new List<EntityChange>();
        });

        var result = await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                return await BuildService(auditLog).GetListAsync(input);
            }
        });

        result.TotalCount.ShouldBe(1);
        var row = result.Items.ShouldHaveSingleItem();
        row.EntityType.ShouldBe("Injury Detail");
        row.PropertyName.ShouldBe("DateOfInjury");
    }

    /// <summary>
    /// The field-name filter is a substring match, unlike the equality used for entity type and
    /// change type.
    /// </summary>
    [Fact]
    public async Task GetListAsync_field_name_filter_matches_a_substring()
    {
        var appointmentId = Guid.NewGuid();
        var when = new DateTime(2026, 6, 8, 8, 0, 0, DateTimeKind.Utc);

        // An INTERIOR fragment, so neither Equals nor StartsWith would match it.
        var input = new GetAppointmentChangeLogsInput { FieldName = "anelnum" };

        // Both properties are allowlisted, so without the filter this yields two rows.
        var (auditLog, _) = StubAuditLog(scan =>
            scan.EntityTypeFullName == AppointmentAuditedEntities.Appointment
                ? new List<EntityChange>
                {
                    Change(AppointmentAuditedEntities.Appointment, appointmentId.ToString(),
                        EntityChangeType.Updated, when,
                        ("PanelNumber", "TEST-P1", "TEST-P2"),
                        ("DueDate", "2026-06-10", "2026-06-12")),
                }
                : new List<EntityChange>());

        var result = await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                return await BuildService(auditLog).GetListAsync(input);
            }
        });

        result.TotalCount.ShouldBe(1);
        result.Items.ShouldHaveSingleItem().PropertyName.ShouldBe("PanelNumber");
    }

    /// <summary>
    /// TotalCount is counted after filtering but BEFORE paging, and the page is taken from the
    /// newest-first sequence.
    /// </summary>
    [Fact]
    public async Task GetListAsync_reports_the_prefiltered_total_and_pages_newest_first()
    {
        var appointmentId = Guid.NewGuid();
        var first = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        var middle = new DateTime(2026, 6, 8, 8, 0, 0, DateTimeKind.Utc);
        var last = new DateTime(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc);
        var input = new GetAppointmentChangeLogsInput { SkipCount = 1, MaxResultCount = 1 };

        // Supplied middle-first, so skipping one WITHOUT sorting lands on `first` while sorting
        // descending lands on `middle`. Feeding them already ordered would make the ordering
        // unobservable through this window.
        var (auditLog, _) = StubAuditLog(scan =>
            scan.EntityTypeFullName == AppointmentAuditedEntities.Appointment
                ? new List<EntityChange>
                {
                    Change(AppointmentAuditedEntities.Appointment, appointmentId.ToString(),
                        EntityChangeType.Updated, middle, ("PanelNumber", "TEST-P2", "TEST-P3")),
                    Change(AppointmentAuditedEntities.Appointment, appointmentId.ToString(),
                        EntityChangeType.Updated, first, ("PanelNumber", "TEST-P1", "TEST-P2")),
                    Change(AppointmentAuditedEntities.Appointment, appointmentId.ToString(),
                        EntityChangeType.Updated, last, ("PanelNumber", "TEST-P3", "TEST-P4")),
                }
                : new List<EntityChange>());

        var result = await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                return await BuildService(auditLog).GetListAsync(input);
            }
        });

        result.TotalCount.ShouldBe(3);
        result.Items.ShouldHaveSingleItem().ChangeTime.ShouldBe(middle);
    }

    /// <summary>
    /// PHI redaction is enforced at the service exit, not only inside the builder's own unit
    /// tests.
    /// </summary>
    [Fact]
    public async Task GetListAsync_masks_phi_before_rows_leave_the_service()
    {
        var appointmentId = Guid.NewGuid();
        var when = new DateTime(2026, 6, 8, 8, 0, 0, DateTimeKind.Utc);
        var input = new GetAppointmentChangeLogsInput();

        // Values are PRESENT on the sensitive field, so masking has something to remove; against
        // null old/new values a deleted mask would be indistinguishable from an applied one.
        // Both names are real Appointment columns; both values are synthetic placeholders.
        var (auditLog, _) = StubAuditLog(scan =>
            scan.EntityTypeFullName == AppointmentAuditedEntities.Appointment
                ? new List<EntityChange>
                {
                    Change(AppointmentAuditedEntities.Appointment, appointmentId.ToString(),
                        EntityChangeType.Updated, when,
                        ("PatientSocialSecurityNumber", "TEST-SSN-OLD", "TEST-SSN-NEW"),
                        ("PatientId", "TEST-A", "TEST-B")),
                }
                : new List<EntityChange>());

        var result = await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                return await BuildService(auditLog).GetListAsync(input);
            }
        });

        // This overlaps AppointmentChangeLogBuilderTests by design but does not duplicate it: the
        // builder tests call the builder directly, so they stay green if the AppService stops
        // routing through it. This one fails on exactly that mutation.
        var row = result.Items.ShouldHaveSingleItem();
        row.PropertyName.ShouldBe("PatientSocialSecurityNumber");
        row.ValueRedacted.ShouldBeTrue();
        row.OldValue.ShouldBeNull();
        row.NewValue.ShouldBeNull();
        result.Items.ShouldNotContain(r => r.PropertyName == "PatientId");
    }

    // ---------------------------------------------------------------------------------------
    // Fixture
    // ---------------------------------------------------------------------------------------

    /// <summary>The audit-query shape the service asked for, captured per call.</summary>
    private sealed record AuditScan(
        string? Sorting,
        int MaxResultCount,
        int SkipCount,
        Guid? AuditLogId,
        DateTime? StartTime,
        DateTime? EndTime,
        EntityChangeType? ChangeType,
        string? EntityId,
        string? EntityTypeFullName,
        bool IncludeDetails);

    /// <summary>Ids for one appointment's intake children, so helpers stay inside the parameter budget.</summary>
    private sealed record IntakeChildIds(Guid InjuryId, Guid BodyPartId, Guid ExaminerId, Guid InsuranceId)
    {
        public static IntakeChildIds New()
            => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
    }

    private static string NewToken() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// A substituted audit repository that records every query shape and optionally answers with
    /// rows chosen from that shape. Every parameter is matched, so the setup survives the service
    /// changing which named arguments it passes.
    /// </summary>
    private static (IAuditLogRepository Repository, List<AuditScan> Scans) StubAuditLog(
        Func<AuditScan, List<EntityChange>>? rowsFor = null)
    {
        var scans = new List<AuditScan>();
        var repository = Substitute.For<IAuditLogRepository>();

        repository.GetEntityChangeListAsync(
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<Guid?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<EntityChangeType?>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                // Positional indices follow IAuditLogRepository.GetEntityChangeListAsync:
                // sorting, maxResultCount, skipCount, auditLogId, startTime, endTime, changeType,
                // entityId, entityTypeFullName, includeDetails, cancellationToken.
                var scan = new AuditScan(
                    ci.ArgAt<string>(0),
                    ci.ArgAt<int>(1),
                    ci.ArgAt<int>(2),
                    ci.ArgAt<Guid?>(3),
                    ci.ArgAt<DateTime?>(4),
                    ci.ArgAt<DateTime?>(5),
                    ci.ArgAt<EntityChangeType?>(6),
                    ci.ArgAt<string>(7),
                    ci.ArgAt<string>(8),
                    ci.ArgAt<bool>(9));
                scans.Add(scan);
                return Task.FromResult(rowsFor?.Invoke(scan) ?? new List<EntityChange>());
            });

        return (repository, scans);
    }

    /// <summary>
    /// The service under test with the five REAL intake repositories and the substituted audit
    /// repository. LazyServiceProvider is assigned by hand because manual construction skips
    /// ABP's property injection, and the inherited AsyncExecuter resolves through it.
    /// </summary>
    private AppointmentChangeLogsAppService BuildService(IAuditLogRepository auditLogRepository)
        => new(
            auditLogRepository,
            _appointmentRepository,
            _injuryRepository,
            _bodyPartRepository,
            _claimExaminerRepository,
            _primaryInsuranceRepository)
        {
            LazyServiceProvider = _lazyServiceProvider,
        };

    /// <summary>
    /// Runs GetListAsync inside TenantA with an audit repository that returns nothing, and hands
    /// back the recorded query shapes. For the tests whose subject is WHICH queries were issued.
    /// </summary>
    private async Task<List<AuditScan>> ListAsync(GetAppointmentChangeLogsInput input)
    {
        var (auditLog, scans) = StubAuditLog();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await BuildService(auditLog).GetListAsync(input);
            }
        });

        return scans;
    }

    /// <summary>
    /// Builds one EntityChange through ABP's public constructor, which is what populates the
    /// PropertyChanges collection ToRaw reads.
    /// </summary>
    private EntityChange Change(
        string entityTypeFullName,
        string entityId,
        EntityChangeType changeType,
        DateTime changeTime,
        params (string Property, string? OldValue, string? NewValue)[] properties)
    {
        var info = new EntityChangeInfo
        {
            ChangeTime = changeTime,
            ChangeType = changeType,
            EntityId = entityId,
            EntityTypeFullName = entityTypeFullName,
            PropertyChanges = properties
                .Select(p => new EntityPropertyChangeInfo
                {
                    PropertyName = p.Property,
                    OriginalValue = p.OldValue,
                    NewValue = p.NewValue,
                    PropertyTypeFullName = "System.String",
                })
                .ToList(),
        };

        // auditLogId is never read back by this service; any value will do.
        return new EntityChange(_guidGenerator, Guid.NewGuid(), info);
    }

    /// <summary>
    /// Inserts one injury detail (with one body part under it), one claim examiner and one primary
    /// insurance for the given appointment, all stamped into TenantA. Must be called inside a unit
    /// of work; autoSave is on so a later unit of work can query the rows back.
    /// </summary>
    private async Task SeedIntakeChildrenAsync(Guid appointmentId, IntakeChildIds ids, string label)
    {
        var tenantId = TenantsTestData.TenantARef;

        await _injuryRepository.InsertAsync(
            new AppointmentInjuryDetail(
                id: ids.InjuryId,
                appointmentId: appointmentId,
                dateOfInjury: new DateTime(2026, 1, 15),
                claimNumber: "TEST-CLM-" + label,
                isCumulativeInjury: false,
                bodyPartsSummary: "TEST-SUMMARY-" + label,
                wcabAdj: "TEST-ADJ-" + label)
            {
                TenantId = tenantId,
            },
            autoSave: true);

        await _bodyPartRepository.InsertAsync(
            new AppointmentBodyPart(ids.BodyPartId, ids.InjuryId, "TEST-BODYPART-" + label)
            {
                TenantId = tenantId,
            },
            autoSave: true);

        await _claimExaminerRepository.InsertAsync(
            new AppointmentClaimExaminer(ids.ExaminerId, appointmentId, isActive: true)
            {
                TenantId = tenantId,
            },
            autoSave: true);

        await _primaryInsuranceRepository.InsertAsync(
            new AppointmentPrimaryInsurance(ids.InsuranceId, appointmentId, isActive: true)
            {
                TenantId = tenantId,
            },
            autoSave: true);
    }
}
