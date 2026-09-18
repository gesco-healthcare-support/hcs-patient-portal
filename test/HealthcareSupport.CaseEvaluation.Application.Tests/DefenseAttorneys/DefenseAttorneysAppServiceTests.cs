using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.DefenseAttorneys;

/// <summary>
/// Covers <see cref="IDefenseAttorneysAppService"/>, which reported 0.0% before this file.
///
/// <para>SAME HARNESS SHAPE AS THE SIX SERVICES IN #977 -- repository plus Manager, lookup
/// endpoints, and one business guard on delete. Nothing here is novel; it is deliberately the
/// cheapest of the tranche to write for exactly that reason.</para>
///
/// <para>THE SERVICE HAS ONE RULE OF ITS OWN and it is the only thing in this file worth calling a
/// guard: <c>DeleteAsync</c> refuses while any <c>AppointmentDefenseAttorney</c> row references the
/// attorney. Create and Update delegate straight to <c>DefenseAttorneyManager</c>, whose
/// <c>Check.Length</c> calls belong to the Manager's own tests, so no Fact below pretends to assert
/// a rule this class does not enforce.</para>
///
/// <para>NO AUTHORIZATION FACTS -- <c>AddAlwaysAllowAuthorization()</c> makes every
/// <c>[Authorize]</c> here inert, so a refusal could not be observed even if it were written.</para>
///
/// <para>THE RIG ACCUMULATES: one SQLite connection for the whole collection and no rollback. Every
/// Fact creates its own rows behind a unique token and filters by it. No Fact asserts a bare
/// <c>TotalCount</c>, because that would be decided by whatever ran before it.</para>
/// </summary>
public abstract class DefenseAttorneysAppServiceTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IDefenseAttorneysAppService _defenseAttorneys;
    private readonly IRepository<AppointmentDefenseAttorney, Guid> _linkRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    protected DefenseAttorneysAppServiceTests()
    {
        _defenseAttorneys = GetRequiredService<IDefenseAttorneysAppService>();
        _linkRepository = GetRequiredService<IRepository<AppointmentDefenseAttorney, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _dataFilter = GetRequiredService<IDataFilter>();
    }

    // FirmName is the filterable column these Facts discriminate on, so the token goes there.
    // Unlike the ClaimExaminer DTO, DefenseAttorneyCreateDto.Email carries only [StringLength] and
    // NOT [EmailAddress], so an arbitrary token would be accepted there -- but the token is put in
    // FirmName anyway because that is the field GetListAsync actually filters by.
    private static string Token(string label) => $"TEST-da-{label}-{Guid.NewGuid():N}"[..36];

    private const string TenDigitPhone = "2135550134";

    /// <summary>
    /// Creates an attorney with NO linked login. <c>IdentityUserId</c> is deliberately left null:
    /// it is a real foreign key to <c>AbpUsers</c>, so an invented Guid cannot be persisted, and
    /// "login optional" is the documented BUG-042 / UM4 behaviour anyway.
    /// </summary>
    private async Task<DefenseAttorneyDto> CreateAsync(string firmName)
    {
        return await _defenseAttorneys.CreateAsync(new DefenseAttorneyCreateDto
        {
            FirstName = "TEST-First",
            LastName = "TEST-Last",
            FirmName = firmName,
            PhoneNumber = TenDigitPhone,
            City = "TEST-City",
        });
    }

    // ------------------------------------------------------------------------
    // Create / read / update.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_PersistsTheAttorneyIncludingTheNamesBug042Added()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var firmName = Token("create");

            var created = await CreateAsync(firmName);

            created.ShouldNotBeNull();
            created.Id.ShouldNotBe(Guid.Empty);
            created.FirmName.ShouldBe(firmName);
            // BUG-042 / UM4 (2026-06-05) is specifically that First/Last name are persisted -- the
            // manager already accepted them and the service was dropping them. Asserting the firm
            // name alone would pass with that fix reverted.
            created.FirstName.ShouldBe("TEST-First");
            created.LastName.ShouldBe("TEST-Last");
        }
    }

    [Fact]
    public async Task CreateAsync_WithNoLinkedLogin_IsAccepted()
    {
        // "Identity now optional" is the other half of BUG-042 / UM4. A record with no login must
        // be creatable, or an attorney who has not registered cannot be entered at all.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateAsync(Token("nologin"));

            // THE DTO NARROWS THE ENTITY, and that is worth pinning rather than working around.
            // DefenseAttorney.IdentityUserId is Guid? ("login optional"), but DefenseAttorneyDto
            // .IdentityUserId is a plain Guid -- so "no login" reaches the SPA as Guid.Empty, not
            // as null. A client checking for null would never see one. Asserting Guid.Empty pins
            // the contract that actually ships; it is not a concession to the type system.
            created.IdentityUserId.ShouldBe(Guid.Empty);
        }
    }

    [Fact]
    public async Task GetAsync_ReturnsThePersistedRow()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var firmName = Token("get");
            var created = await CreateAsync(firmName);

            var fetched = await _defenseAttorneys.GetAsync(created.Id);

            fetched.Id.ShouldBe(created.Id);
            fetched.FirmName.ShouldBe(firmName);
        }
    }

    [Fact]
    public async Task GetWithNavigationPropertiesAsync_ReturnsTheAttorneyAndItsNavigationShape()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var firmName = Token("nav");
            var created = await CreateAsync(firmName);

            var fetched = await _defenseAttorneys.GetWithNavigationPropertiesAsync(created.Id);

            fetched.ShouldNotBeNull();
            fetched.DefenseAttorney.ShouldNotBeNull();
            fetched.DefenseAttorney.FirmName.ShouldBe(firmName);
        }
    }

    [Fact]
    public async Task UpdateAsync_PersistsTheChange()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateAsync(Token("update-before"));
            var newFirmName = Token("update-after");

            var updated = await _defenseAttorneys.UpdateAsync(created.Id, new DefenseAttorneyUpdateDto
            {
                FirstName = "TEST-Updated-First",
                LastName = "TEST-Last",
                FirmName = newFirmName,
                PhoneNumber = TenDigitPhone,
                City = "TEST-City",
                ConcurrencyStamp = created.ConcurrencyStamp,
            });

            updated.FirmName.ShouldBe(newFirmName);
            updated.FirstName.ShouldBe("TEST-Updated-First");

            // Re-read rather than trusting the returned DTO, which the mapper could produce from the
            // in-memory entity without the write having reached the database.
            var reread = await _defenseAttorneys.GetAsync(created.Id);
            reread.FirmName.ShouldBe(newFirmName);
        }
    }

    // ------------------------------------------------------------------------
    // GetListAsync.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetListAsync_FiltersByFirmName()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var mine = Token("filter-mine");
            await CreateAsync(mine);
            await CreateAsync(Token("filter-other"));

            var page = await _defenseAttorneys.GetListAsync(new GetDefenseAttorneysInput
            {
                FirmName = mine,
                MaxResultCount = 10,
            });

            // Asserting the CONTENTS, not the count: the rig accumulates across the collection, so
            // a count assertion would be decided by whatever else has run.
            page.Items.ShouldContain(x => x.DefenseAttorney.FirmName == mine);
            page.Items.ShouldAllBe(x => x.DefenseAttorney.FirmName == mine);
        }
    }

    [Fact]
    public async Task GetListAsync_WithAFilterThatMatchesNothing_ReturnsAnEmptyPage()
    {
        // The other direction of the same filter. Without this, a service that ignored FirmName
        // entirely would still satisfy the ShouldContain above.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await CreateAsync(Token("nomatch-seed"));

            var page = await _defenseAttorneys.GetListAsync(new GetDefenseAttorneysInput
            {
                FirmName = Token("matches-nothing"),
                MaxResultCount = 10,
            });

            page.Items.ShouldBeEmpty(
                "A filter nothing matches must return nothing. Items coming back here means "
                + "FirmName is not reaching the query at all.");
        }
    }

    // ------------------------------------------------------------------------
    // Lookups.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetStateLookupAsync_ReturnsStatesAndHonoursTheFilter()
    {
        // STATES ARE HOST-SEEDED AND IMultiTenant. Reading them from inside a tenant returns
        // nothing unless the tenant filter is disabled -- a repo erratum that cost four failing
        // tests in tranche 1 before it was traced, so it is disabled explicitly here rather than
        // rediscovered. The comment in the seed contributor claiming State is host-only is wrong;
        // State.cs:13 declares IMultiTenant.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var all = await _defenseAttorneys.GetStateLookupAsync(new LookupRequestDto
            {
                MaxResultCount = 100,
            });

            all.Items.ShouldNotBeEmpty("States are seeded; an empty lookup means the query is "
                + "being tenant-filtered and the caller gets an empty dropdown.");

            var seeded = all.Items.First();
            var filtered = await _defenseAttorneys.GetStateLookupAsync(new LookupRequestDto
            {
                Filter = seeded.DisplayName,
                MaxResultCount = 100,
            });

            filtered.Items.ShouldContain(x => x.Id == seeded.Id);
        }
    }

    [Fact]
    public async Task GetIdentityUserLookupAsync_FiltersOnEmail()
    {
        // The lookup filters on Email, not on name. Uses a seeded user so the Fact does not depend
        // on creating a login of its own.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var filtered = await _defenseAttorneys.GetIdentityUserLookupAsync(new LookupRequestDto
            {
                Filter = IdentityUsersTestData.ClaimExaminer1Email,
                MaxResultCount = 50,
            });

            filtered.Items.ShouldNotBeEmpty(
                "A filter matching a seeded user's email must return that user.");

            var unmatched = await _defenseAttorneys.GetIdentityUserLookupAsync(new LookupRequestDto
            {
                Filter = "TEST-no-such-email-anywhere@test.local",
                MaxResultCount = 50,
            });

            unmatched.Items.ShouldBeEmpty(
                "Both directions, or a service ignoring Filter would satisfy the assertion above.");
        }
    }

    // ------------------------------------------------------------------------
    // DeleteAsync -- the only guard this service owns.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_WhenNoAppointmentReferencesTheAttorney_Succeeds()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateAsync(Token("delete-ok"));

            await Should.NotThrowAsync(async () => await _defenseAttorneys.DeleteAsync(created.Id));

            await Should.ThrowAsync<Exception>(
                async () => await _defenseAttorneys.GetAsync(created.Id),
                "The row must actually be gone, not merely un-refused.");
        }
    }

    [Fact]
    public async Task DeleteAsync_WhileAnAppointmentReferencesTheAttorney_IsRefused()
    {
        // THE HIGHEST-VALUE FACT IN THIS FILE, and the fixture is the point: the link row is what
        // the guard exists to detect. Against an unreferenced attorney the guard cannot fire and
        // deleting it would look identical -- which is exactly what the Fact above would show.
        //
        // What it protects: an appointment whose defense attorney row has been deleted underneath
        // it. Prompt 15 / item 32.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateAsync(Token("delete-inuse"));

            await _linkRepository.InsertAsync(
                new AppointmentDefenseAttorney(
                    Guid.NewGuid(),
                    AppointmentsTestData.Appointment1Id,
                    created.Id,
                    identityUserId: null),
                autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(
                async () => await _defenseAttorneys.DeleteAsync(created.Id));

            ex.Code.ShouldBe(
                CaseEvaluationDomainErrorCodes.DefenseAttorneyInUse,
                "The refusal must carry the in-use code. A different BusinessException here means "
                + "something else refused and this Fact is not testing the guard it names.");

            // The attorney must still be there. Without this, a guard that threw AFTER deleting
            // would pass the assertion above while having done the damage anyway.
            var survivor = await _defenseAttorneys.GetAsync(created.Id);
            survivor.Id.ShouldBe(created.Id);
        }
    }
}
