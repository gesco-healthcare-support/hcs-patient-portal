using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// The seam: <c>GetExternalUserLookupAsync</c> and <c>GetMyProfileAsync</c>. This is the HIPAA
/// surface of the service. The lookup must never behave as a directory: it requires a search term,
/// and an external caller is scoped to the co-parties named on appointments they can already see
/// rather than to the office roster. The profile reports only one of the four EXTERNAL role names,
/// so an internal role never leaks through a field the SPA treats as "which external party is
/// this".
///
/// <para>NOT pinned here: authorization. <c>AddAlwaysAllowAuthorization()</c> neutralises every
/// <c>[Authorize]</c> in the test module, so no test in this file asserts an authorization
/// failure. The one exception-shaped test below asserts the service's OWN hand-written
/// null-user-id guard, which is real code and not the attribute.</para>
///
/// <para>Also not pinned: the co-party collection rule itself (<c>ExternalCoPartyRules</c>) and the
/// visible-appointment computation (<c>AppointmentVisibilityService</c>); both have their own
/// tests. They are reached here only to pin that this service routes an external caller through
/// them instead of through the office search.</para>
/// </summary>
public abstract class ExternalSignupLookupTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string Password = "Test1234!";

    private readonly IExternalSignupAppService _appService;
    private readonly IdentityUserManager _userManager;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principal;

    protected ExternalSignupLookupTests()
    {
        _appService = GetRequiredService<IExternalSignupAppService>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _patientRepository = GetRequiredService<IRepository<Patient, Guid>>();
        _appointmentRepository = GetRequiredService<IRepository<Appointment, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private static string NewToken() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// HIPAA: no blanket listing. A blank search term returns nothing even though the office has
    /// external users to return -- which the first assertion establishes, so the emptiness below
    /// cannot be an artefact of an empty office.
    /// </summary>
    [Fact]
    public async Task GetExternalUserLookupAsync_BlankFilter_ReturnsNothing()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var withTerm = await _appService.GetExternalUserLookupAsync(
                IdentityUsersTestData.Patient1Email);
            withTerm.Items.ShouldContain(i => i.IdentityUserId == IdentityUsersTestData.Patient1UserId);

            (await _appService.GetExternalUserLookupAsync(null)).Items.ShouldBeEmpty();
            (await _appService.GetExternalUserLookupAsync("   ")).Items.ShouldBeEmpty();
        }
    }

    /// <summary>
    /// The highest-value rule in this file. An external caller is relationship-scoped, never
    /// office-scoped: the same search term that returns a patient to internal staff must return
    /// nothing to an external user who shares no appointment with that patient.
    /// </summary>
    [Fact]
    public async Task GetExternalUserLookupAsync_ExternalOnlyCaller_DoesNotGetTheTenantSearch()
    {
        var token = NewToken();
        var attorneyEmail = $"ext-{token}@test.local";

        await _appService.RegisterAsync(new ExternalUserSignUpDto
        {
            UserType = ExternalUserType.ApplicantAttorney,
            Email = attorneyEmail,
            Password = Password,
            ConfirmPassword = Password,
            FirmName = $"TEST-Firm-{token}",
            TenantId = TenantsTestData.TenantARef,
        });

        Guid externalUserId;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var account = await _userManager.FindByEmailAsync(attorneyEmail);
            account.ShouldNotBeNull();
            externalUserId = account!.Id;
        }

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            // Internal staff: the seeded patient IS reachable by this exact term.
            using (WithCurrentUser.Run(
                       _principal,
                       IdentityUsersTestData.TenantAdmin1UserId,
                       IdentityUsersTestData.TenantAdminRoleName))
            {
                var staffResult = await _appService.GetExternalUserLookupAsync(
                    IdentityUsersTestData.Patient1Email);
                staffResult.Items.ShouldContain(
                    i => i.IdentityUserId == IdentityUsersTestData.Patient1UserId);
            }

            // Same office, same term, external-only caller with no shared appointment.
            // RunWithEmail, not Run: the co-party rule reads ICurrentUser.Email.
            using (WithCurrentUser.RunWithEmail(
                       _principal,
                       externalUserId,
                       attorneyEmail,
                       IdentityUsersTestData.ApplicantAttorneyRoleName))
            {
                var externalResult = await _appService.GetExternalUserLookupAsync(
                    IdentityUsersTestData.Patient1Email);
                externalResult.Items.ShouldBeEmpty();
            }
        }
    }

    /// <summary>
    /// The staff search never offers the caller themselves. The row exists and is returned to a
    /// different caller using the identical term, so the exclusion is what removes it.
    /// </summary>
    [Fact]
    public async Task GetExternalUserLookupAsync_StaffSearch_ExcludesTheCallerThemselves()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            using (WithCurrentUser.Run(
                       _principal,
                       IdentityUsersTestData.TenantAdmin1UserId,
                       IdentityUsersTestData.TenantAdminRoleName))
            {
                var seenByAnother = await _appService.GetExternalUserLookupAsync(
                    IdentityUsersTestData.Patient1Email);
                seenByAnother.Items.ShouldContain(
                    i => i.IdentityUserId == IdentityUsersTestData.Patient1UserId);
            }

            // Carrying an internal role too, so the STAFF branch is taken and the co-party branch
            // is not what empties the result.
            using (WithCurrentUser.Run(
                       _principal,
                       IdentityUsersTestData.Patient1UserId,
                       IdentityUsersTestData.TenantAdminRoleName,
                       IdentityUsersTestData.PatientRoleName))
            {
                var seenByThemselves = await _appService.GetExternalUserLookupAsync(
                    IdentityUsersTestData.Patient1Email);
                seenByThemselves.Items.ShouldNotContain(
                    i => i.IdentityUserId == IdentityUsersTestData.Patient1UserId);
            }
        }
    }

    /// <summary>
    /// The co-party branch itself: an external caller finds the registered parties named on an
    /// appointment they can see, each tagged with the role that appointment column represents.
    /// The whole fixture is created by this test so nothing depends on another test's rows.
    /// </summary>
    [Fact]
    public async Task GetExternalUserLookupAsync_CoPartyBranch_ReturnsRegisteredCoPartiesTaggedByRole()
    {
        var token = NewToken();
        var patientEmail = $"cp-pt-{token}@test.local";
        var attorneyEmail = $"cp-aa-{token}@test.local";

        await _appService.RegisterAsync(new ExternalUserSignUpDto
        {
            UserType = ExternalUserType.Patient,
            Email = patientEmail,
            Password = Password,
            ConfirmPassword = Password,
            TenantId = TenantsTestData.TenantARef,
        });

        await _appService.RegisterAsync(new ExternalUserSignUpDto
        {
            UserType = ExternalUserType.ApplicantAttorney,
            Email = attorneyEmail,
            Password = Password,
            ConfirmPassword = Password,
            FirmName = $"TEST-Firm-{token}",
            TenantId = TenantsTestData.TenantARef,
        });

        // This whole block needs an ambient unit of work: it drives IdentityUserManager and two
        // repositories directly, and the RegisterAsync calls above ended with autoSave:true writes
        // that dispose their own context. Without the wrapper the first read here throws
        // ObjectDisposedException, which is how this Fact failed before. Calling the AppService
        // needs no wrapper -- ABP wraps app services itself -- which is why only the direct
        // repository work is inside it.
        var patientUserId = Guid.Empty;
        var attorneyUserId = Guid.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var patientAccount = await _userManager.FindByEmailAsync(patientEmail);
            patientAccount.ShouldNotBeNull();
            patientUserId = patientAccount!.Id;

            var attorneyAccount = await _userManager.FindByEmailAsync(attorneyEmail);
            attorneyAccount.ShouldNotBeNull();
            attorneyUserId = attorneyAccount!.Id;

            var patientMaster = await _patientRepository.FirstOrDefaultAsync(
                p => p.Email == patientEmail);
            patientMaster.ShouldNotBeNull();

            // One shared appointment naming both parties. Slot2 is the free seeded slot in
            // TenantA; the confirmation number is token-keyed because (TenantId, number) is unique.
            await _appointmentRepository.InsertAsync(
                new Appointment(
                    id: Guid.NewGuid(),
                    patientId: patientMaster!.Id,
                    identityUserId: patientUserId,
                    appointmentTypeId: LocationsTestData.AppointmentType1Id,
                    locationId: LocationsTestData.Location1Id,
                    doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot2Id,
                    appointmentDate: new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc),
                    requestConfirmationNumber: $"A9C{token}",
                    appointmentStatus: AppointmentStatusType.Pending)
                {
                    PatientEmail = patientEmail,
                    ApplicantAttorneyEmail = attorneyEmail,
                },
                autoSave: true);
        }
        });

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(
                   _principal,
                   patientUserId,
                   patientEmail,
                   IdentityUsersTestData.PatientRoleName))
        {
            var result = await _appService.GetExternalUserLookupAsync($"cp-aa-{token}");

            var coParty = result.Items.SingleOrDefault(i => i.IdentityUserId == attorneyUserId);
            coParty.ShouldNotBeNull();
            coParty!.Email.ShouldBe(attorneyEmail);
            coParty.UserRole.ShouldBe(IdentityUsersTestData.ApplicantAttorneyRoleName);
            coParty.FirmName.ShouldBe($"TEST-Firm-{token}");
        }
    }

    /// <summary>
    /// <c>UserRole</c> on this DTO means "which of the four external parties is this", so the
    /// predicate is a whitelist and not "whatever role happens to come back first". The caller
    /// here DOES hold a role, so an unconditional first-role read would report it.
    /// </summary>
    [Fact]
    public async Task GetMyProfileAsync_InternalRole_IsNotReportedAsAnExternalRole()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var staffAccount = await _userManager.GetByIdAsync(
                IdentityUsersTestData.TenantAdmin1UserId);
            var roles = await _userManager.GetRolesAsync(staffAccount);
            roles.ShouldContain(IdentityUsersTestData.TenantAdminRoleName);

            using (WithCurrentUser.Run(
                       _principal,
                       IdentityUsersTestData.TenantAdmin1UserId,
                       IdentityUsersTestData.TenantAdminRoleName))
            {
                var profile = await _appService.GetMyProfileAsync();
                profile.IdentityUserId.ShouldBe(IdentityUsersTestData.TenantAdmin1UserId);
                profile.UserRole.ShouldBeEmpty();
            }
        }
    }

    /// <summary>
    /// The register-to-profile round trip for the extension properties the SPA routes on: the
    /// firm a firm account displays instead of a personal name, and the external flag that sends
    /// the user to the external home rather than the internal dashboard.
    /// </summary>
    [Fact]
    public async Task GetMyProfileAsync_RegisteredAttorney_SurfacesFirmNameAndIsExternalUser()
    {
        var token = NewToken();
        var email = $"prf-{token}@test.local";
        var firmName = $"TEST-Firm-{token}";

        await _appService.RegisterAsync(new ExternalUserSignUpDto
        {
            UserType = ExternalUserType.DefenseAttorney,
            Email = email,
            Password = Password,
            ConfirmPassword = Password,
            FirmName = firmName,
            TenantId = TenantsTestData.TenantARef,
        });

        Guid newUserId;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var account = await _userManager.FindByEmailAsync(email);
            account.ShouldNotBeNull();
            newUserId = account!.Id;
        }

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.Run(
                   _principal,
                   newUserId,
                   IdentityUsersTestData.DefenseAttorneyRoleName))
        {
            var profile = await _appService.GetMyProfileAsync();
            profile.Email.ShouldBe(email);
            profile.UserRole.ShouldBe(IdentityUsersTestData.DefenseAttorneyRoleName);
            profile.FirmName.ShouldBe(firmName);
            profile.IsExternalUser.ShouldBeTrue();
            profile.IsAccessor.ShouldBeFalse();
        }
    }

    /// <summary>
    /// The service's OWN null-user-id guard, not the <c>[Authorize]</c> attribute -- always-allow
    /// is active, so the attribute contributes nothing here. Without the guard the next line
    /// dereferences a null id and the caller gets an opaque InvalidOperationException instead of
    /// an authorization failure.
    /// </summary>
    [Fact]
    public async Task GetMyProfileAsync_UnauthenticatedPrincipal_ThrowsAuthorization()
    {
        using (_principal.Change(new ClaimsPrincipal(new ClaimsIdentity())))
        {
            await Should.ThrowAsync<Volo.Abp.Authorization.AbpAuthorizationException>(
                () => _appService.GetMyProfileAsync());
        }
    }
}
