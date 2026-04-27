using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentStatuses;

public abstract class AppointmentStatusesAppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAppointmentStatusesAppService _statusesAppService;
    private readonly IAppointmentStatusRepository _statusRepository;
    private readonly AppointmentStatusManager _statusManager;

    protected AppointmentStatusesAppServiceTests()
    {
        _statusesAppService = GetRequiredService<IAppointmentStatusesAppService>();
        _statusRepository = GetRequiredService<IAppointmentStatusRepository>();
        _statusManager = GetRequiredService<AppointmentStatusManager>();
    }

    // ------------------------------------------------------------------------
    // CRUD happy path -- AppointmentStatus is host-only; no CurrentTenant wrap.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_ReturnsSeededStatus1()
    {
        var result = await _statusesAppService.GetAsync(AppointmentStatusesTestData.Status1Id);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(AppointmentStatusesTestData.Status1Id);
        result.Name.ShouldBe(AppointmentStatusesTestData.Status1Name);
    }

    [Fact]
    public async Task GetListAsync_ReturnsAllSeededStatuses()
    {
        var result = await _statusesAppService.GetListAsync(new GetAppointmentStatusesInput
        {
            MaxResultCount = 100
        });

        result.Items.Any(x => x.Id == AppointmentStatusesTestData.Status1Id).ShouldBeTrue();
        result.Items.Any(x => x.Id == AppointmentStatusesTestData.Status2Id).ShouldBeTrue();
    }

    [Fact]
    public async Task GetListAsync_FiltersByFilterText()
    {
        // GetAppointmentStatusesInput exposes only FilterText (no Name field);
        // the AppService's GetListAsync passes it through to the repository.
        var result = await _statusesAppService.GetListAsync(new GetAppointmentStatusesInput
        {
            FilterText = "Approved",
            MaxResultCount = 100
        });

        result.Items.Any(x => x.Id == AppointmentStatusesTestData.Status2Id).ShouldBeTrue();
        result.Items.Any(x => x.Id == AppointmentStatusesTestData.Status1Id).ShouldBeFalse();
    }

    [Fact]
    public async Task CreateAsync_PersistsNewStatus()
    {
        var input = new AppointmentStatusCreateDto { Name = "TEST-CreatedScratchStatus" };

        var created = await _statusesAppService.CreateAsync(input);

        created.ShouldNotBeNull();
        created.Name.ShouldBe(input.Name);

        var persisted = await _statusRepository.FindAsync(created.Id);
        persisted.ShouldNotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_ChangesMutableFields()
    {
        var existing = await _statusRepository.GetAsync(AppointmentStatusesTestData.Status2Id);
        var update = new AppointmentStatusUpdateDto { Name = "TEST-MutatedApproved" };

        var result = await _statusesAppService.UpdateAsync(AppointmentStatusesTestData.Status2Id, update);
        result.Name.ShouldBe("TEST-MutatedApproved");

        // Restore seed value.
        var current = await _statusRepository.GetAsync(AppointmentStatusesTestData.Status2Id);
        current.Name = AppointmentStatusesTestData.Status2Name;
        await _statusRepository.UpdateAsync(current);
    }

    [Fact]
    public async Task DeleteAsync_RemovesStatus()
    {
        var created = await _statusesAppService.CreateAsync(new AppointmentStatusCreateDto
        {
            Name = "TEST-ToDeleteStatus"
        });

        await _statusesAppService.DeleteAsync(created.Id);

        (await _statusRepository.FindAsync(created.Id)).ShouldBeNull();
    }

    // ------------------------------------------------------------------------
    // Bulk-delete coverage -- both methods feed into the same Delete permission.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task DeleteByIdsAsync_RemovesMultipleStatuses()
    {
        // Create 2 ad-hoc rows; bulk-delete by ID list. Seeded Status1/2 keep
        // their Ids out of the list so they remain intact.
        var first = await _statusesAppService.CreateAsync(new AppointmentStatusCreateDto
        {
            Name = "TEST-BulkScratch1"
        });
        var second = await _statusesAppService.CreateAsync(new AppointmentStatusCreateDto
        {
            Name = "TEST-BulkScratch2"
        });

        await _statusesAppService.DeleteByIdsAsync(new List<Guid> { first.Id, second.Id });

        (await _statusRepository.FindAsync(first.Id)).ShouldBeNull();
        (await _statusRepository.FindAsync(second.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAllAsync_FilterScopedToScratchOnly_PreservesSeed()
    {
        // DeleteAllAsync takes a FilterText that scopes the bulk delete to
        // matching rows only. Use a unique scratch marker so the seeded
        // Status1/2 (TEST-PendingLabel / TEST-ApprovedLabel) do NOT match the
        // filter and remain intact -- no shared-seed-wipe risk.
        const string scratchMarker = "TEST-DeleteAllScratchMarker";

        await _statusesAppService.CreateAsync(new AppointmentStatusCreateDto { Name = scratchMarker });
        await _statusesAppService.CreateAsync(new AppointmentStatusCreateDto { Name = scratchMarker });

        await _statusesAppService.DeleteAllAsync(new GetAppointmentStatusesInput
        {
            FilterText = scratchMarker
        });

        var afterDelete = await _statusesAppService.GetListAsync(new GetAppointmentStatusesInput
        {
            FilterText = scratchMarker,
            MaxResultCount = 100
        });
        afterDelete.Items.ShouldBeEmpty();

        // Seeded rows untouched.
        (await _statusRepository.FindAsync(AppointmentStatusesTestData.Status1Id)).ShouldNotBeNull();
        (await _statusRepository.FindAsync(AppointmentStatusesTestData.Status2Id)).ShouldNotBeNull();
    }

    [Fact]
    public async Task Manager_CreateAsync_WhenNameExceedsMax_ThrowsArgumentException()
    {
        // NameMaxLength = 100. A 101-char name triggers Check.Length.
        var oversizedName = new string('a', 101);

        await Should.ThrowAsync<ArgumentException>(() =>
            _statusManager.CreateAsync(oversizedName));
    }

    // ------------------------------------------------------------------------
    // Entity-vs-enum gap encoding -- the AppointmentStatus entity is parallel
    // to the AppointmentStatusType enum used on Appointment.AppointmentStatus.
    // There is NO FK from Appointment to this entity; adding entity rows does
    // not affect appointment behaviour. No canonical ABP test pattern (external
    // research Track 2). Encoded as Skip Fact.
    // ------------------------------------------------------------------------

    [Fact(Skip = "GAP: AppointmentStatuses entity is parallel to AppointmentStatusType enum "
              + "but Appointment.AppointmentStatus uses the enum directly with NO FK to the "
              + "entity table. Adding entity rows does not affect appointment behaviour, and "
              + "adding enum values does not require an entity row. No canonical ABP pattern "
              + "for testing this divergence (external research Track 2). Tracked: "
              + "src/.../Domain/AppointmentStatuses/CLAUDE.md Known Gotchas #1-#2 AND "
              + "docs/issues/INCOMPLETE-FEATURES.md (anchor TBD).")]
    public Task Appointment_StatusEnum_AndAppointmentStatusEntity_AreNotLinked()
    {
        // Target behaviour: when a developer creates a new AppointmentStatus
        // entity row, the corresponding AppointmentStatusType enum value should
        // require an updated entity row (or vice-versa) for consistency. Today
        // the two are completely independent at the database level.
        return Task.CompletedTask;
    }
}
