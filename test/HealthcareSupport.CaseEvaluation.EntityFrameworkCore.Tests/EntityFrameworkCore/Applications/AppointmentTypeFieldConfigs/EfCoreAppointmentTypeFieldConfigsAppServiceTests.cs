using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentTypeFieldConfigs;

/// <summary>
/// Integration coverage for <see cref="AppointmentTypeFieldConfigsAppService"/> -- the admin
/// Field Configuration panel's read/write seam and the booking form's per-AppointmentType
/// config read.
///
/// <para>WHY A NEW FILE RATHER THAN MORE FACTS IN <c>FieldConfigReconcilerTests</c>. That file
/// is a plain class with no base and no DI; it reaches <c>FieldConfigReconciler</c> (an
/// <c>internal static</c> helper) through <c>InternalsVisibleTo</c> and calls nothing else. It
/// pins the PURE diff -- which rows to create, update and delete. It structurally cannot reach
/// the APPLICATION of that plan: the delete/update/create loops, the explicit unit-of-work
/// flush, the read-back, or any of the three CreateAsync guards. Those need the ABP harness,
/// which is what this class supplies. Nothing here needs Application-assembly internals, so the
/// abstract-body-plus-thin-runner split is unnecessary and this is a single concrete class
/// (same shape as <c>EfCoreExternalAccountAppServiceTests</c>).</para>
///
/// <para>ISOLATION. <c>SaveForAppointmentTypeAsync</c> is a replace-set that DELETES rows absent
/// from the desired set, so two Facts sharing one AppointmentType would destroy each other's
/// rows. Every Fact therefore mints its own AppointmentType and its own random token, and every
/// assertion filters by ids or field names that Fact created itself. No Fact asserts on a COUNT
/// of the table.</para>
///
/// <para>WHAT THESE TESTS DO NOT PIN.</para>
/// <list type="bullet">
/// <item>AUTHORIZATION. The service carries six <c>[Authorize]</c> gates (a plain one at class
/// level plus CustomFields Default/Create/Edit/Delete per method). The test module registers
/// <c>AddAlwaysAllowAuthorization()</c>, so every gate is a no-op in-process and a
/// permission-denied assertion could not fail. None is written here.</item>
/// <item>THE <c>items ??= new List&lt;...&gt;()</c> NULL COALESCE (service line 129). It is dead
/// through the public surface: <c>ApplicationService</c> implements <c>IValidationEnabled</c>, so
/// ABP's <c>MethodInvocationValidator</c> rejects a null argument for a non-optional,
/// non-primitive parameter and raises <c>AbpValidationException</c> before the method body runs.
/// A Fact passing <c>items: null!</c> would assert the framework validator while appearing to
/// assert the service.</item>
/// <item>THE <c>_unitOfWorkManager.Current == null</c> FALSE BRANCH (service line 161).
/// <c>ApplicationService</c> is <c>IUnitOfWorkEnabled</c>, so a unit of work is always ambient
/// inside the call. Only the true branch is reachable.</item>
/// <item>THE BLANK-FieldName FILTER inside the batch (service line 139). It is fully redundant
/// with <c>FieldConfigReconciler</c>'s own blank drop, which
/// <c>Reconcile_BlankFieldNames_AreIgnored</c> already pins; deleting the service's
/// <c>.Where(...)</c> changes no observable outcome, so an end-to-end Fact on it would close a
/// line without pinning a rule.</item>
/// <item>CROSS-AppointmentType ORDERING (<c>OrderBy(x =&gt; x.AppointmentTypeId)</c>). Proving it
/// needs rows on AppointmentTypes this Fact does not own, on a rig other Facts write to.</item>
/// <item>A DELETE-NOT-FOUND THROW. ABP's <c>RepositoryBase.DeleteAsync(TKey)</c> is a silent
/// no-op for an id that does not exist, so there is no throw to assert.</item>
/// </list>
///
/// <para>NO IMPERSONATION IS NEEDED. This service reads neither <c>ICurrentUser.Id</c> nor
/// <c>.Email</c> and takes no internal-role short circuit, so the ambient default principal is
/// irrelevant to every rule below. The only ambient context that matters is the tenant, and the
/// one Fact that cares changes it explicitly.</para>
///
/// <para>NO EVENTS, NO NOTIFICATIONS. The service injects only the repository, the manager and
/// <c>IUnitOfWorkManager</c> -- no event bus, no notification sender, no template render. So no
/// assertion here has to be hoisted out of a unit-of-work lambda to see a deferred publish, and
/// the notification-template seeding wall is never reached.</para>
///
/// <para>All data is synthetic: <c>TEST-</c> prefixed identifiers and neutral form-field keys.
/// No Fact stores anything resembling a patient identifier.</para>
/// </summary>
public class EfCoreAppointmentTypeFieldConfigsAppServiceTests
    : CaseEvaluationApplicationTestBase<CaseEvaluationEntityFrameworkCoreTestModule>
{
    private readonly IAppointmentTypeFieldConfigsAppService _fieldConfigsAppService;
    private readonly IAppointmentTypesAppService _appointmentTypesAppService;
    private readonly IRepository<AppointmentTypeFieldConfig, Guid> _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    public EfCoreAppointmentTypeFieldConfigsAppServiceTests()
    {
        _fieldConfigsAppService = GetRequiredService<IAppointmentTypeFieldConfigsAppService>();
        _appointmentTypesAppService = GetRequiredService<IAppointmentTypesAppService>();
        _repository = GetRequiredService<IRepository<AppointmentTypeFieldConfig, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _dataFilter = GetRequiredService<IDataFilter>();
    }

    private static string Token() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Mints an AppointmentType owned by the calling Fact.
    ///
    /// <para>A FRESH AppointmentType PER FACT IS THE ISOLATION PRIMITIVE, not a convenience.
    /// The seeded AppointmentType1/2 rows are shared, and <c>SaveForAppointmentTypeAsync</c>
    /// deletes every config row on the AppointmentType it is given, so a Fact that used a shared
    /// type would silently wipe another Fact's fixture.</para>
    ///
    /// <para>The <c>TEST-atfc-</c> prefix is deliberate: it cannot collide with
    /// <c>GetListAsync_FiltersByName_ReturnsMatchingType</c>, which filters AppointmentTypes on
    /// "Orthopedic".</para>
    /// </summary>
    private async Task<Guid> NewAppointmentTypeAsync(string token, string suffix = "")
    {
        var created = await _appointmentTypesAppService.CreateAsync(new AppointmentTypeCreateDto
        {
            Name = $"TEST-atfc-{token}{suffix}"
        });
        return created.Id;
    }

    /// <summary>
    /// Creates one config row through the service's own public surface.
    ///
    /// <para>CALLED AT TOP LEVEL, NEVER INSIDE A SHARED <c>WithUnitOfWorkAsync</c> LAMBDA. The
    /// duplicate guard in <c>CreateAsync</c> is <c>query.Any(...)</c>, a database query; EF Core
    /// does not flush a pending insert before evaluating it. Two creates batched into one
    /// enclosing unit of work would therefore both see an empty table. Calling at top level lets
    /// ABP's unit-of-work interceptor give each call its own unit of work and commit it.</para>
    /// </summary>
    private Task<AppointmentTypeFieldConfigDto> NewConfigAsync(
        Guid appointmentTypeId,
        string fieldName,
        bool hidden = false,
        bool readOnly = false,
        bool required = false,
        string? defaultValue = null)
    {
        return _fieldConfigsAppService.CreateAsync(new AppointmentTypeFieldConfigCreateDto
        {
            AppointmentTypeId = appointmentTypeId,
            FieldName = fieldName,
            Hidden = hidden,
            ReadOnly = readOnly,
            Required = required,
            DefaultValue = defaultValue
        });
    }

    private static AppointmentTypeFieldConfigBatchItemDto Item(
        string fieldName,
        bool hidden = false,
        bool readOnly = false,
        bool required = false,
        string? defaultValue = null)
    {
        return new AppointmentTypeFieldConfigBatchItemDto
        {
            FieldName = fieldName,
            Hidden = hidden,
            ReadOnly = readOnly,
            Required = required,
            DefaultValue = defaultValue
        };
    }

    // ------------------------------------------------------------------------
    // CreateAsync -- one happy path plus all three guards. Unlike
    // AppointmentsAppService, these guards ARE reachable: the three field-config
    // DTOs carry no DataAnnotations and the repo's only FluentValidation
    // AbstractValidator targets AppointmentCreateDto, so nothing runs ahead of
    // the service's own checks.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_PersistsEveryFlagAndDefaultValue_AndMapsThemBack()
    {
        var token = Token();
        var typeId = await NewAppointmentTypeAsync(token);
        var fieldName = $"TEST-{token}-claimNumber";

        var created = await NewConfigAsync(
            typeId, fieldName, hidden: true, readOnly: true, required: true, defaultValue: "TEST-dv");

        created.ShouldNotBeNull();
        created.AppointmentTypeId.ShouldBe(typeId);
        created.FieldName.ShouldBe(fieldName);
        created.Hidden.ShouldBeTrue();
        created.ReadOnly.ShouldBeTrue();
        created.Required.ShouldBeTrue();
        created.DefaultValue.ShouldBe("TEST-dv");

        // Second call, so this half reads the PERSISTED row rather than the
        // in-memory entity the create returned. All three flags are asserted
        // true, which is what makes a dropped argument in the manager call (or a
        // MapperIgnoreTarget on the DTO projection) visible rather than silently
        // absorbed by a false default.
        var reread = await _fieldConfigsAppService.GetAsync(created.Id);

        reread.ShouldNotBeNull();
        reread.FieldName.ShouldBe(fieldName);
        reread.Hidden.ShouldBeTrue();
        reread.ReadOnly.ShouldBeTrue();
        reread.Required.ShouldBeTrue();
        reread.DefaultValue.ShouldBe("TEST-dv");
    }

    [Fact]
    public async Task CreateAsync_WhenAppointmentTypeIdIsEmpty_ThrowsUserFriendly()
    {
        var token = Token();

        // Without the guard the flow reaches the manager, which inserts a row whose
        // AppointmentTypeId FK points at nothing. SQLite FK enforcement is ON in this
        // rig, so the failure would be a DbUpdateException at unit-of-work completion --
        // a 500, not the field-is-required message the admin panel expects.
        await Should.ThrowAsync<UserFriendlyException>(
            () => _fieldConfigsAppService.CreateAsync(new AppointmentTypeFieldConfigCreateDto
            {
                AppointmentTypeId = Guid.Empty,
                FieldName = $"TEST-{token}-claimNumber"
            }));
    }

    [Fact]
    public async Task CreateAsync_WhenFieldNameIsBlank_ThrowsUserFriendly()
    {
        var token = Token();
        var typeId = await NewAppointmentTypeAsync(token);

        // Whitespace, not empty string: IsNullOrWhiteSpace is the guard, and a
        // blank-but-present name is the realistic admin-panel input.
        //
        // The backstop is AppointmentTypeFieldConfigManager's
        // Check.NotNullOrWhiteSpace(fieldName, ...), which throws ArgumentException.
        // That is a different type, so removing the service guard fails this Fact
        // rather than being masked by the manager.
        await Should.ThrowAsync<UserFriendlyException>(
            () => _fieldConfigsAppService.CreateAsync(new AppointmentTypeFieldConfigCreateDto
            {
                AppointmentTypeId = typeId,
                FieldName = "   "
            }));
    }

    [Fact]
    public async Task CreateAsync_WhenSameTypeAndFieldNameExists_ThrowsUserFriendly()
    {
        var token = Token();
        var typeId = await NewAppointmentTypeAsync(token);
        var fieldName = $"TEST-{token}-employerName";

        var first = await NewConfigAsync(typeId, fieldName);
        first.ShouldNotBeNull();

        // THE SERVICE GUARD IS THE ONLY DUPLICATE PROTECTION THAT APPLIES HERE. The
        // composite unique index on (TenantId, AppointmentTypeId, FieldName) is filtered
        // to "[TenantId] IS NOT NULL AND [IsDeleted] = 0"
        // (CaseEvaluationSharedModelConfiguration.cs:785 + the shared filter constant at
        // line 74). These rows are created in the host context, where TenantId is null,
        // so the index is inert and the database would happily accept the second row.
        await Should.ThrowAsync<UserFriendlyException>(
            () => _fieldConfigsAppService.CreateAsync(new AppointmentTypeFieldConfigCreateDto
            {
                AppointmentTypeId = typeId,
                FieldName = fieldName
            }));
    }

    // ------------------------------------------------------------------------
    // Read members.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetByAppointmentTypeIdAsync_ReturnsOnlyRowsForThatAppointmentType()
    {
        var token = Token();
        var typeA = await NewAppointmentTypeAsync(token, "-a");
        var typeB = await NewAppointmentTypeAsync(token, "-b");
        var fieldName = $"TEST-{token}-interpreter";

        var onA = await NewConfigAsync(typeA, fieldName);

        // LOAD-BEARING FIXTURE, NOT SETUP NOISE. This is the row the Where clause is
        // meant to EXCLUDE. Without it the Fact would assert only what the filter keeps
        // and would pass with the filter deleted.
        var onB = await NewConfigAsync(typeB, fieldName);

        var rows = await _fieldConfigsAppService.GetByAppointmentTypeIdAsync(typeA);

        rows.Any(x => x.Id == onA.Id).ShouldBeTrue();
        rows.Any(x => x.Id == onB.Id).ShouldBeFalse(
            "GetByAppointmentTypeIdAsync must filter to the requested AppointmentType; the "
            + "booking form applies whatever comes back to its own form rows.");
    }

    [Fact]
    public async Task GetListAsync_WhenAppointmentTypeIdSupplied_ExcludesOtherTypes()
    {
        var token = Token();
        var typeA = await NewAppointmentTypeAsync(token, "-a");
        var typeB = await NewAppointmentTypeAsync(token, "-b");
        var fieldName = $"TEST-{token}-attorneyName";

        var onA = await NewConfigAsync(typeA, fieldName);
        var onB = await NewConfigAsync(typeB, fieldName);

        var rows = await _fieldConfigsAppService.GetListAsync(typeA);

        rows.Any(x => x.Id == onA.Id).ShouldBeTrue();
        rows.Any(x => x.Id == onB.Id).ShouldBeFalse(
            "the optional filter's true branch must narrow the list to one AppointmentType.");
    }

    [Fact]
    public async Task GetListAsync_WhenAppointmentTypeIdIsNull_ReturnsRowsAcrossTypes()
    {
        var token = Token();
        var typeA = await NewAppointmentTypeAsync(token, "-a");
        var typeB = await NewAppointmentTypeAsync(token, "-b");
        var fieldName = $"TEST-{token}-bodyPart";

        var onA = await NewConfigAsync(typeA, fieldName);
        var onB = await NewConfigAsync(typeB, fieldName);

        var rows = await _fieldConfigsAppService.GetListAsync(appointmentTypeId: null);

        // Superset assertions only. The rig is shared and accumulates, so a count of the
        // unfiltered list would be a count of rows this Fact did not create.
        rows.Any(x => x.Id == onA.Id).ShouldBeTrue(
            "a null filter must not narrow the list -- the admin panel's unfiltered view "
            + "depends on the HasValue false branch.");
        rows.Any(x => x.Id == onB.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task GetListAsync_OrdersByFieldNameWithinAnAppointmentType()
    {
        var token = Token();
        var typeId = await NewAppointmentTypeAsync(token);

        var zulu = $"TEST-{token}-zulu";
        var alpha = $"TEST-{token}-alpha";
        var mike = $"TEST-{token}-mike";

        // Inserted deliberately out of alphabetical order, so insertion order and sorted
        // order differ. The suffixes are lowercase ASCII with a common prefix, so SQLite's
        // default BINARY collation gives one unambiguous expected sequence.
        await NewConfigAsync(typeId, zulu);
        await NewConfigAsync(typeId, alpha);
        await NewConfigAsync(typeId, mike);

        var rows = await _fieldConfigsAppService.GetListAsync(typeId);

        rows.Select(x => x.FieldName).ShouldBe(new[] { alpha, mike, zulu });
    }

    [Fact]
    public async Task GetAsync_WhenIdIsUnknown_ThrowsEntityNotFound()
    {
        // COVERAGE-ORIENTED, and labelled as such. This pins the choice of
        // _repository.GetAsync over FindAsync -- a real distinction (GetAsync throws;
        // FindAsync would return null and the ObjectMapper would then fail on a null
        // source with a less useful error) but a thin one.
        await Should.ThrowAsync<EntityNotFoundException>(
            () => _fieldConfigsAppService.GetAsync(Guid.NewGuid()));
    }

    // ------------------------------------------------------------------------
    // UpdateAsync / DeleteAsync.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_ChangesEveryMutableValue()
    {
        var token = Token();
        var typeId = await NewAppointmentTypeAsync(token);
        var fieldName = $"TEST-{token}-dateOfInjury";

        var created = await NewConfigAsync(
            typeId, fieldName, hidden: false, readOnly: false, required: false, defaultValue: null);

        // The row must start at the OPPOSITE of every target value, or "it is true
        // afterwards" would be satisfied by a service that forwards nothing.
        created.Hidden.ShouldBeFalse();
        created.ReadOnly.ShouldBeFalse();
        created.Required.ShouldBeFalse();
        created.DefaultValue.ShouldBeNull();

        var updated = await _fieldConfigsAppService.UpdateAsync(
            created.Id,
            new AppointmentTypeFieldConfigUpdateDto
            {
                Hidden = true,
                ReadOnly = true,
                Required = true,
                DefaultValue = "TEST-dv2",
                ConcurrencyStamp = null
            });

        updated.Id.ShouldBe(created.Id);
        updated.Hidden.ShouldBeTrue();
        updated.ReadOnly.ShouldBeTrue();
        updated.Required.ShouldBeTrue();
        updated.DefaultValue.ShouldBe("TEST-dv2");

        var reread = await _fieldConfigsAppService.GetAsync(created.Id);
        reread.Hidden.ShouldBeTrue();
        reread.ReadOnly.ShouldBeTrue();
        reread.Required.ShouldBeTrue();
        reread.DefaultValue.ShouldBe("TEST-dv2");
    }

    [Fact]
    public async Task UpdateAsync_WhenConcurrencyStampIsStale_ThrowsConcurrencyFailure()
    {
        var token = Token();
        var typeId = await NewAppointmentTypeAsync(token);

        var created = await NewConfigAsync(typeId, $"TEST-{token}-panelNumber");

        // 32 hex chars, matching ABP's own stamp shape and inside the 40-char column.
        var staleStamp = Guid.NewGuid().ToString("N");
        staleStamp.ShouldNotBe(created.ConcurrencyStamp);

        // The manager forwards input.ConcurrencyStamp through
        // SetConcurrencyStampIfNotNull, and ABP copies the entity's stamp into the
        // update's OriginalValue, so a wrong stamp makes the generated WHERE match zero
        // rows. Passing null instead (the mutation this Fact exists to catch) disables
        // the check entirely and the update silently succeeds.
        var ex = await Should.ThrowAsync<Exception>(
            () => _fieldConfigsAppService.UpdateAsync(
                created.Id,
                new AppointmentTypeFieldConfigUpdateDto
                {
                    Hidden = true,
                    ReadOnly = false,
                    Required = false,
                    DefaultValue = null,
                    ConcurrencyStamp = staleStamp
                }));

        // Accept either the raw EF Core exception or ABP's wrapper. Production catches
        // the wrapper (AppointmentsAppService.cs:804), so that is the expected shape --
        // but this Fact exists to pin the FORWARDING of the stamp, not the framework's
        // choice of wrapper, and the mutation it guards produces NO exception at all.
        (ex is AbpDbConcurrencyException || ex is DbUpdateConcurrencyException).ShouldBeTrue(
            $"expected a concurrency failure, got {ex.GetType().FullName}: {ex.Message}");
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheRowFromEveryRead()
    {
        var token = Token();
        var typeId = await NewAppointmentTypeAsync(token);
        var fieldName = $"TEST-{token}-wcabOffice";

        var created = await NewConfigAsync(typeId, fieldName);

        // Prove the row is visible BEFORE the delete, so "absent afterwards" cannot be
        // satisfied by a row that was never there.
        var before = await _fieldConfigsAppService.GetByAppointmentTypeIdAsync(typeId);
        before.Any(x => x.Id == created.Id).ShouldBeTrue();

        await _fieldConfigsAppService.DeleteAsync(created.Id);

        var after = await _fieldConfigsAppService.GetByAppointmentTypeIdAsync(typeId);
        after.Any(x => x.Id == created.Id).ShouldBeFalse(
            "a soft-deleted config row must not reach the booking form.");

        await Should.ThrowAsync<EntityNotFoundException>(
            () => _fieldConfigsAppService.GetAsync(created.Id));
    }

    // ------------------------------------------------------------------------
    // SaveForAppointmentTypeAsync -- the replace-set batch save behind the admin
    // Field Configuration panel. The pure plan is FieldConfigReconciler's and is
    // pinned in FieldConfigReconcilerTests; what follows pins the APPLICATION of
    // that plan, one loop per Fact, plus the flush.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task SaveForAppointmentTypeAsync_DeletesRowsAbsentFromTheDesiredSet()
    {
        var token = Token();
        var typeId = await NewAppointmentTypeAsync(token);
        var keepName = $"TEST-{token}-keep";
        var dropName = $"TEST-{token}-drop";

        var keep = await NewConfigAsync(typeId, keepName);

        // LOAD-BEARING FIXTURE. A negative guarantee cannot be proven against an empty
        // set: this is the row the ToDelete loop is meant to remove. Without it the loop
        // iterates nothing and the Fact would pass with the loop deleted.
        var drop = await NewConfigAsync(typeId, dropName);

        var returned = await _fieldConfigsAppService.SaveForAppointmentTypeAsync(
            typeId,
            new List<AppointmentTypeFieldConfigBatchItemDto> { Item(keepName) });

        returned.Any(x => x.Id == keep.Id).ShouldBeTrue();
        returned.Any(x => x.Id == drop.Id).ShouldBeFalse();

        // Separate call, so this half reads committed state rather than the same unit of
        // work's change tracker.
        var after = await _fieldConfigsAppService.GetByAppointmentTypeIdAsync(typeId);
        after.Any(x => x.Id == keep.Id).ShouldBeTrue();
        after.Any(x => x.Id == drop.Id).ShouldBeFalse(
            "a field removed from the panel must be gone after the save, not merely hidden "
            + "from the response.");
    }

    [Fact]
    public async Task SaveForAppointmentTypeAsync_UpdatesAChangedRowInPlaceKeepingItsId()
    {
        var token = Token();
        var typeId = await NewAppointmentTypeAsync(token);
        var fieldName = $"TEST-{token}-panelRow";

        var seeded = await NewConfigAsync(
            typeId, fieldName, hidden: false, readOnly: false, required: false, defaultValue: null);
        seeded.Hidden.ShouldBeFalse();
        seeded.Required.ShouldBeFalse();

        var returned = await _fieldConfigsAppService.SaveForAppointmentTypeAsync(
            typeId,
            new List<AppointmentTypeFieldConfigBatchItemDto>
            {
                Item(fieldName, hidden: true, readOnly: true, required: true, defaultValue: "TEST-dv4")
            });

        var row = returned.SingleOrDefault(x => x.FieldName == fieldName);
        row.ShouldNotBeNull();

        // THE ID HALF IS NOT DECORATION. It is what distinguishes an in-place update from
        // a delete-then-recreate: the row is [Audited], so recreating it would discard the
        // audit trail and the original CreationTime on every panel save.
        row!.Id.ShouldBe(seeded.Id);
        row.Hidden.ShouldBeTrue();
        row.ReadOnly.ShouldBeTrue();
        row.Required.ShouldBeTrue();
        row.DefaultValue.ShouldBe("TEST-dv4");
    }

    [Fact]
    public async Task SaveForAppointmentTypeAsync_CreatesRowsForNewFieldNames()
    {
        var token = Token();
        var typeId = await NewAppointmentTypeAsync(token);
        var existingName = $"TEST-{token}-existing";
        var addedName = $"TEST-{token}-added";

        var existing = await NewConfigAsync(typeId, existingName);

        await _fieldConfigsAppService.SaveForAppointmentTypeAsync(
            typeId,
            new List<AppointmentTypeFieldConfigBatchItemDto>
            {
                Item(existingName),
                Item(addedName, hidden: true, required: true, defaultValue: "TEST-dv3")
            });

        // Asserted through a SEPARATE call rather than the return value, deliberately.
        // That keeps this Fact about the ToCreate loop alone; the in-call visibility of
        // the same write is what
        // SaveForAppointmentTypeAsync_ReturnValueReflectsTheWriteInTheSameCall owns.
        var after = await _fieldConfigsAppService.GetByAppointmentTypeIdAsync(typeId);

        var added = after.SingleOrDefault(x => x.FieldName == addedName);
        added.ShouldNotBeNull();
        added!.AppointmentTypeId.ShouldBe(typeId);
        added.Hidden.ShouldBeTrue();
        added.Required.ShouldBeTrue();
        added.DefaultValue.ShouldBe("TEST-dv3");

        after.Any(x => x.Id == existing.Id).ShouldBeTrue(
            "a name present in both sets is an unchanged row, not a delete plus a create.");
    }

    [Fact]
    public async Task SaveForAppointmentTypeAsync_ReturnValueReflectsTheWriteInTheSameCall()
    {
        var token = Token();
        var typeId = await NewAppointmentTypeAsync(token);
        var keptName = $"TEST-{token}-kept";
        var freshName = $"TEST-{token}-fresh";

        await NewConfigAsync(typeId, keptName);

        var returned = await _fieldConfigsAppService.SaveForAppointmentTypeAsync(
            typeId,
            new List<AppointmentTypeFieldConfigBatchItemDto> { Item(keptName), Item(freshName) });

        // THIS ASSERTION IS ABOUT THE EXPLICIT FLUSH AND NOTHING ELSE, which is why it
        // reads the RETURNED list rather than re-reading afterwards. The read-back is a
        // database query; a newly INSERTED row sits in the change tracker until
        // SaveChangesAsync runs, and the ambient unit of work does not commit until after
        // the method returns. Drop the flush block and the panel gets its own pre-save
        // state back as confirmation of the save. A Fact that only re-read afterwards
        // would pass against that.
        returned.Any(x => x.FieldName == freshName).ShouldBeTrue(
            "the response must include rows created by this very call; without the explicit "
            + "SaveChangesAsync the read-back queries the database before the insert reaches it.");
        returned.Any(x => x.FieldName == keptName).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveForAppointmentTypeAsync_WhenAppointmentTypeIdIsEmpty_ThrowsUserFriendly()
    {
        // An EMPTY items list, not a populated one: with nothing to write, deleting the
        // guard produces a clean "returned an empty list, threw nothing" rather than an
        // incidental foreign-key error that would keep the Fact green for the wrong
        // reason.
        //
        // Guid is primitive-extended, so ABP's MethodInvocationValidator passes
        // Guid.Empty straight through and the service guard is genuinely reachable.
        await Should.ThrowAsync<UserFriendlyException>(
            () => _fieldConfigsAppService.SaveForAppointmentTypeAsync(
                Guid.Empty,
                new List<AppointmentTypeFieldConfigBatchItemDto>()));
    }

    // ------------------------------------------------------------------------
    // Tenant boundary.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_UnderATenant_IsInvisibleToTheHostRead()
    {
        // TenantARef is captured at seed time, so it is read HERE, in the body -- never
        // from a static initializer, where it would still be Guid.Empty.
        var tenantId = TenantsTestData.TenantARef;
        tenantId.ShouldNotBe(Guid.Empty, "the tenant seed must have run before this Fact.");

        var token = Token();

        // The AppointmentType is created in the host. The foreign key is enforced by the
        // database, which does not consult the multi-tenancy query filter, so a
        // tenant-scoped config row can still reference it.
        var typeId = await NewAppointmentTypeAsync(token);
        var fieldName = $"TEST-{token}-tenantScoped";

        AppointmentTypeFieldConfigDto created;
        using (_currentTenant.Change(tenantId))
        {
            created = await NewConfigAsync(typeId, fieldName);
        }

        // NOTE WHAT THIS DOES AND DOES NOT PROVE. It confirms the row was written under
        // tenant A, which is what stops the host-read assertion below passing vacuously
        // against a row that never existed. It does NOT isolate the service's
        // CurrentTenant.Id argument, because ABP stamps IMultiTenant.TenantId from the
        // ambient tenant at insert time anyway.
        created.TenantId.ShouldBe(tenantId);

        var hostRows = await _fieldConfigsAppService.GetByAppointmentTypeIdAsync(typeId);
        hostRows.Any(x => x.Id == created.Id).ShouldBeFalse(
            "a tenant's field configuration must not reach a host-context read of the same "
            + "AppointmentType. This pins AppointmentTypeFieldConfig being IMultiTenant, not "
            + "service code -- the service has no tenant branch of its own.");

        // The row IS there. Disabling the filter is the only way to see it from the host,
        // and seeing it is what makes the negative assertion above meaningful.
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var persisted = await _repository.FindAsync(created.Id);
            persisted.ShouldNotBeNull();
            persisted!.TenantId.ShouldBe(tenantId);
            persisted.FieldName.ShouldBe(fieldName);
        }
    }
}
