using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Branding;

/// <summary>
/// 2026-10-09 (walkthrough Q5) -- the in-office packet letterhead editor. The security-relevant
/// part is isolation: the row is host-side and keyed by office, so an edit in one office must
/// land on that office's row and never another's. Branding is host-only, so the shared-SQLite
/// rig holds both offices' rows the way production's host database does.
/// </summary>
public abstract class OfficeLetterheadAppServiceTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IOfficeLetterheadAppService _letterhead;
    private readonly ICurrentTenant _currentTenant;
    private readonly IRepository<OfficeBranding, Guid> _brandingRepository;

    protected OfficeLetterheadAppServiceTests()
    {
        _letterhead = GetRequiredService<IOfficeLetterheadAppService>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _brandingRepository = GetRequiredService<IRepository<OfficeBranding, Guid>>();
    }

    [Fact]
    public async Task An_office_that_never_set_a_letterhead_gets_its_doctor_as_the_default()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var dto = await _letterhead.GetAsync();

                var expected = $"Dr. {DoctorsTestData.Doctor1FirstName} {DoctorsTestData.Doctor1LastName}";
                dto.PhysicianName.ShouldBeNull();
                dto.DefaultPhysicianName.ShouldBe(expected);
                dto.DefaultLetterheadName.ShouldBe(expected);
            }
        });
    }

    [Fact]
    public async Task Update_stores_on_this_offices_host_row_and_reads_back()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var dto = await _letterhead.UpdateAsync(new UpdateOfficeLetterheadInput
                {
                    PhysicianName = "  TEST-Ada Example, M.D. ",
                    Phone = "555-0100",
                    MissedAppointmentFee = 99.5m,
                });

                dto.PhysicianName.ShouldBe("TEST-Ada Example, M.D.");
                dto.Phone.ShouldBe("555-0100");
                dto.MissedAppointmentFee.ShouldBe(99.5m);
                // A blank heading now falls back to the physician the office typed.
                dto.DefaultLetterheadName.ShouldBe("TEST-Ada Example, M.D.");
            }
        });

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                var row = await _brandingRepository.FirstOrDefaultAsync(x => x.OfficeId == TenantsTestData.TenantARef);
                row.ShouldNotBeNull();
                row.Phone.ShouldBe("555-0100");
            }
        });
    }

    [Fact]
    public async Task An_edit_in_one_office_never_reaches_another()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await _letterhead.UpdateAsync(new UpdateOfficeLetterheadInput { PracticeName = "TEST Alpha Institute" });
            }
            using (_currentTenant.Change(TenantsTestData.TenantBRef))
            {
                await _letterhead.UpdateAsync(new UpdateOfficeLetterheadInput { PracticeName = "TEST Beta Institute" });
            }
        });

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                (await _letterhead.GetAsync()).PracticeName.ShouldBe("TEST Alpha Institute");
            }
            using (_currentTenant.Change(TenantsTestData.TenantBRef))
            {
                var b = await _letterhead.GetAsync();
                b.PracticeName.ShouldBe("TEST Beta Institute");
                b.DefaultPhysicianName.ShouldBe(
                    $"Dr. {DoctorsTestData.Doctor2FirstName} {DoctorsTestData.Doctor2LastName}");
            }
        });
    }

    [Fact]
    public async Task Clearing_a_field_returns_it_to_the_default()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await _letterhead.UpdateAsync(new UpdateOfficeLetterheadInput { Fax = "555-0101" });
                var cleared = await _letterhead.UpdateAsync(new UpdateOfficeLetterheadInput { Fax = "   " });

                cleared.Fax.ShouldBeNull();
            }
        });
    }

    [Fact]
    public async Task Host_scope_has_no_letterhead_to_read_or_edit()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                await Should.ThrowAsync<UserFriendlyException>(() => _letterhead.GetAsync());
                await Should.ThrowAsync<UserFriendlyException>(
                    () => _letterhead.UpdateAsync(new UpdateOfficeLetterheadInput { Phone = "555-0100" }));
            }
        });
    }
}
