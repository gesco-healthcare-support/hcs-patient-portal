using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

public abstract class AppointmentAccessorsAppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAppointmentAccessorsAppService _accessorsAppService;
    private readonly IAppointmentAccessorRepository _accessorRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

    protected AppointmentAccessorsAppServiceTests()
    {
        _accessorsAppService = GetRequiredService<IAppointmentAccessorsAppService>();
        _accessorRepository = GetRequiredService<IAppointmentAccessorRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _dataFilter = GetRequiredService<IDataFilter>();
        _currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    // ------------------------------------------------------------------------
    // CRUD happy path -- Accessor1 lives in TenantA; wrap fetches in TenantA.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_ReturnsSeededAccessor()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _accessorsAppService.GetAsync(AppointmentAccessorsTestData.Accessor1Id);

            result.ShouldNotBeNull();
            result.Id.ShouldBe(AppointmentAccessorsTestData.Accessor1Id);
            result.AccessTypeId.ShouldBe(AppointmentAccessorsTestData.Accessor1AccessType);
            result.AppointmentId.ShouldBe(AppointmentsTestData.Appointment1Id);
            result.IdentityUserId.ShouldBe(IdentityUsersTestData.ApplicantAttorney1UserId);
        }
    }

    [Fact]
    public async Task GetListAsync_FromTenantAContext_ReturnsOnlyAccessor1()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _accessorsAppService.GetListAsync(new GetAppointmentAccessorsInput());

            result.Items.Any(x => x.AppointmentAccessor.Id == AppointmentAccessorsTestData.Accessor1Id).ShouldBeTrue();
            result.Items.Any(x => x.AppointmentAccessor.Id == AppointmentAccessorsTestData.Accessor2Id).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task CreateAsync_AsInternalUser_LinksExistingUserByEmail()
    {
        // Group J: accessors are added by typing an email. An internal caller
        // bypasses the edit-gate; the email resolves to a seeded user so
        // create-or-link LINKS the existing identity (no auto-provision).
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.Run(
                   _currentPrincipalAccessor,
                   IdentityUsersTestData.HostAdminId,
                   IdentityUsersTestData.HostAdminRoleName))
        {
            var input = new AppointmentAccessorCreateDto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                Email = IdentityUsersTestData.Patient1Email,
                Role = IdentityUsersTestData.PatientRoleName,
                AccessTypeId = AccessType.View
            };

            var created = await _accessorsAppService.CreateAsync(input);

            created.ShouldNotBeNull();
            created.AccessTypeId.ShouldBe(AccessType.View);
            created.IdentityUserId.ShouldBe(IdentityUsersTestData.Patient1UserId);
            created.AppointmentId.ShouldBe(input.AppointmentId);

            var persisted = await _accessorRepository.FindAsync(created.Id);
            persisted.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task CreateAsync_WhenCallerIsNonParty_IsDenied()
    {
        // Deny-by-default (Group J): a same-tenant external user who is NOT a
        // party to the appointment (not creator, not an Edit-accessor) must not
        // be able to add an accessor -- blocks self-escalation. The edit-gate
        // fires before create-or-link, so the email never resolves.
        var nonPartyUserId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.Run(
                   _currentPrincipalAccessor,
                   nonPartyUserId,
                   IdentityUsersTestData.PatientRoleName))
        {
            var input = new AppointmentAccessorCreateDto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                Email = "TEST-outsider@test.local",
                Role = IdentityUsersTestData.PatientRoleName,
                AccessTypeId = AccessType.View
            };

            await Should.ThrowAsync<BusinessException>(
                async () => await _accessorsAppService.CreateAsync(input));
        }
    }

    [Fact]
    public async Task UpdateAsync_ChangesMutableFields()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var existing = await _accessorRepository.GetAsync(AppointmentAccessorsTestData.Accessor1Id);
            var update = new AppointmentAccessorUpdateDto
            {
                AccessTypeId = AccessType.Edit, // Flip View -> Edit for this test.
                IdentityUserId = existing.IdentityUserId,
                AppointmentId = existing.AppointmentId
            };

            var result = await _accessorsAppService.UpdateAsync(AppointmentAccessorsTestData.Accessor1Id, update);
            result.AccessTypeId.ShouldBe(AccessType.Edit);

            // Restore seed value.
            var current = await _accessorRepository.GetAsync(AppointmentAccessorsTestData.Accessor1Id);
            current.AccessTypeId = AppointmentAccessorsTestData.Accessor1AccessType;
            await _accessorRepository.UpdateAsync(current);
        }
    }

    [Fact]
    public async Task DeleteAsync_AsInternalUser_RemovesAccessor()
    {
        // Delete is gated by edit-access; an internal caller bypasses the gate.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.Run(
                   _currentPrincipalAccessor,
                   IdentityUsersTestData.HostAdminId,
                   IdentityUsersTestData.HostAdminRoleName))
        {
            var created = await _accessorsAppService.CreateAsync(new AppointmentAccessorCreateDto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                Email = IdentityUsersTestData.Patient1Email,
                Role = IdentityUsersTestData.PatientRoleName,
                AccessTypeId = AccessType.View
            });

            await _accessorsAppService.DeleteAsync(created.Id);

            (await _accessorRepository.FindAsync(created.Id)).ShouldBeNull();
        }
    }

    // ------------------------------------------------------------------------
    // Cross-tenant isolation (ApplicantAttorney / Patient IMultiTenant pattern).
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetListAsync_FromTenantBContext_ReturnsOnlyAccessor2()
    {
        using (_currentTenant.Change(TenantsTestData.TenantBRef))
        {
            var result = await _accessorsAppService.GetListAsync(new GetAppointmentAccessorsInput());

            result.Items.Any(x => x.AppointmentAccessor.Id == AppointmentAccessorsTestData.Accessor2Id).ShouldBeTrue();
            result.Items.Any(x => x.AppointmentAccessor.Id == AppointmentAccessorsTestData.Accessor1Id).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task GetListAsync_FromHostContextWithFilterDisabled_ReturnsAccessorsFromBothTenants()
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var result = await _accessorsAppService.GetListAsync(new GetAppointmentAccessorsInput());

            result.Items.Any(x => x.AppointmentAccessor.Id == AppointmentAccessorsTestData.Accessor1Id).ShouldBeTrue();
            result.Items.Any(x => x.AppointmentAccessor.Id == AppointmentAccessorsTestData.Accessor2Id).ShouldBeTrue();
        }
    }

    // ------------------------------------------------------------------------
    // Lookup endpoints
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetIdentityUserLookupAsync_FiltersByEmail()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _accessorsAppService.GetIdentityUserLookupAsync(new LookupRequestDto
            {
                Filter = "@test.local",
                MaxResultCount = 100
            });

            result.Items.ShouldNotBeEmpty();
            result.Items.Any(x => x.Id == IdentityUsersTestData.ApplicantAttorney1UserId).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task GetAppointmentLookupAsync_ReturnsAppointmentsInTenant()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _accessorsAppService.GetAppointmentLookupAsync(new LookupRequestDto
            {
                MaxResultCount = 100
            });

            result.Items.Any(x => x.Id == AppointmentsTestData.Appointment1Id).ShouldBeTrue();
            // Appointment2 lives in TenantB -- must NOT leak into TenantA's lookup.
            result.Items.Any(x => x.Id == AppointmentsTestData.Appointment2Id).ShouldBeFalse();
        }
    }

    // ------------------------------------------------------------------------
    // DESIGN NOTE, 2026-09-15 -- A ROAD NOT TAKEN. This is not outstanding work.
    //
    // A skipped test used to sit here, holding open the idea that accessor
    // mutations would one day be gated by feature-specific permissions
    // (CaseEvaluation.AppointmentAccessors.{Create,Edit,Delete}). Those constants
    // were defined and granted to roles, but never enforced by anything, and the
    // whole surface was removed on 2026-09-15.
    //
    // The decision was deliberate rather than a cleanup: accessor mutations are
    // gated by AppointmentReadAccessGuard.EnsureCanManageAccessorsAsync, which
    // composes the pure AppointmentAccessRules.CanManageAccessors rule. That gate
    // is STRICTER than a permission would be -- deny-by-default, and it excludes
    // the Edit-accessor pathway that appointment edit-access allows. Swapping it
    // for a role permission would LOOSEN the rule, not tighten it.
    //
    // So there is nothing to resume here. If accessor permissions are ever wanted,
    // that is a new design decision about the authorization model, not the
    // completion of an unfinished one -- and it would start by asking whether a
    // role-level grant can express "the creator, if they also hold an accessor-
    // managing external role", which is what the guard actually enforces.
    // ------------------------------------------------------------------------
}
