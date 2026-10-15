using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.ClaimExaminers;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// The seam: the anonymous half of <c>ExternalSignupAppService.RegisterAsync</c> -- the per-role
/// master record it creates or ADOPTS, the "not provided yet" sentinels it uses instead of
/// fabricating demographics, the back-link to appointments a booker created before the person had
/// a login, and the office scoping of the duplicate-email gate.
///
/// <para>NOT pinned here:</para>
/// <list type="bullet">
///   <item>Authorization, for the usual reason: <c>AddAlwaysAllowAuthorization()</c> makes every
///         attribute on this service a no-op, so an authorization assertion could not fail.</item>
///   <item>The attorney adopt-by-email paths, already covered by
///         <c>ExternalSignupAppServiceTests</c>. The Patient twin of that rule is covered below
///         because it was the branch with no test.</item>
///   <item>Whether the verification email is actually delivered. The rig has no per-office
///         notification templates and the account emailer logs and skips a missing one.</item>
/// </list>
/// </summary>
public abstract class ExternalSignupRegistrationTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string Password = "Test1234!";

    private readonly IExternalSignupAppService _appService;
    private readonly IdentityUserManager _userManager;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IRepository<ClaimExaminer, Guid> _claimExaminerRepository;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principal;

    protected ExternalSignupRegistrationTests()
    {
        _appService = GetRequiredService<IExternalSignupAppService>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _patientRepository = GetRequiredService<IRepository<Patient, Guid>>();
        _claimExaminerRepository = GetRequiredService<IRepository<ClaimExaminer, Guid>>();
        _appointmentRepository = GetRequiredService<IRepository<Appointment, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    // The rig shares one SQLite connection for the whole run and never rolls back, so every
    // fixture here is keyed by a token unique to its own test and no assertion counts rows it
    // did not create.
    private static string NewToken() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// IP6: booking a patient who has no login yet leaves a record-only Patient row keyed by
    /// email. Self-registration must CLAIM that row, not insert a second one next to it.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_Patient_ClaimsUnclaimedRecordOnlyPatientRow()
    {
        var token = NewToken();
        var email = $"clm-{token}@test.local";
        var recordOnlyId = Guid.NewGuid();

        // Seed the very thing the code is meant to adopt. Against an empty fixture the "exactly
        // one row" assertion below would hold even with the adopt lookup deleted.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await _patientRepository.InsertAsync(
                new Patient(
                    id: recordOnlyId,
                    stateId: null,
                    appointmentLanguageId: null,
                    identityUserId: null,
                    tenantId: TenantsTestData.TenantARef,
                    firstName: "TEST-Unclaimed",
                    lastName: "TEST-Record",
                    email: email,
                    genderId: Gender.Unspecified,
                    dateOfBirth: DateTime.MinValue,
                    phoneNumberTypeId: PhoneNumberType.Home),
                autoSave: true);
        }

        await _appService.RegisterAsync(new ExternalUserSignUpDto
        {
            UserType = ExternalUserType.Patient,
            Email = email,
            Password = Password,
            ConfirmPassword = Password,
            TenantId = TenantsTestData.TenantARef,
        });

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var account = await _userManager.FindByEmailAsync(email);
            account.ShouldNotBeNull();

            var rows = await _patientRepository.GetListAsync(p => p.Email == email);
            rows.Count.ShouldBe(1);
            rows[0].Id.ShouldBe(recordOnlyId);
            rows[0].IdentityUserId.ShouldBe(account!.Id);
        }
    }

    /// <summary>
    /// The claim above is only half the job: appointments the booker created against that
    /// record-only patient still carry a null <c>IdentityUserId</c>, and AutoLinkPatientAsync
    /// stamps the new login onto them.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_Patient_BackLinksAppointmentsOfClaimedRow()
    {
        var token = NewToken();
        var email = $"lnk-{token}@test.local";
        var recordOnlyId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await _patientRepository.InsertAsync(
                new Patient(
                    id: recordOnlyId,
                    stateId: null,
                    appointmentLanguageId: null,
                    identityUserId: null,
                    tenantId: TenantsTestData.TenantARef,
                    firstName: "TEST-Booked",
                    lastName: "TEST-Record",
                    email: email,
                    genderId: Gender.Unspecified,
                    dateOfBirth: DateTime.MinValue,
                    phoneNumberTypeId: PhoneNumberType.Home),
                autoSave: true);

            // Slot2 is the one free seeded slot in TenantA; the catalog FKs are host-scoped rows
            // the seed already created. The confirmation number is token-keyed because
            // (TenantId, RequestConfirmationNumber) is unique.
            await _appointmentRepository.InsertAsync(
                new Appointment(
                    id: appointmentId,
                    patientId: recordOnlyId,
                    identityUserId: null,
                    appointmentTypeId: LocationsTestData.AppointmentType1Id,
                    locationId: LocationsTestData.Location1Id,
                    doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot2Id,
                    appointmentDate: new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc),
                    requestConfirmationNumber: $"A9L{token}",
                    appointmentStatus: AppointmentStatusType.Pending),
                autoSave: true);
        }

        await _appService.RegisterAsync(new ExternalUserSignUpDto
        {
            UserType = ExternalUserType.Patient,
            Email = email,
            Password = Password,
            ConfirmPassword = Password,
            TenantId = TenantsTestData.TenantARef,
        });

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var account = await _userManager.FindByEmailAsync(email);
            account.ShouldNotBeNull();

            var appointment = await _appointmentRepository.GetAsync(appointmentId);
            appointment.IdentityUserId.ShouldBe(account!.Id);
        }
    }

    /// <summary>
    /// G-06-08: the minimal register form collects no demographics, so the created Patient must
    /// carry "not provided yet" sentinels. Fabricating a gender (this once defaulted to
    /// <c>Gender.Male</c>) puts an invented clinical fact on a medical record.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_Patient_UsesNotProvidedSentinelsNotFabricatedValues()
    {
        var token = NewToken();
        var email = $"snt-{token}@test.local";

        await _appService.RegisterAsync(new ExternalUserSignUpDto
        {
            UserType = ExternalUserType.Patient,
            Email = email,
            Password = Password,
            ConfirmPassword = Password,
            TenantId = TenantsTestData.TenantARef,
        });

        // The repository read MUST sit inside WithUnitOfWorkAsync. Calling an AppService does not
        // need one (ABP wraps app services itself), but a direct repository call does: without an
        // ambient unit of work the preceding autoSave:true write disposes its own context and this
        // read throws ObjectDisposedException. That is exactly how this Fact failed before.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var created = await _patientRepository.FirstOrDefaultAsync(p => p.Email == email);
                created.ShouldNotBeNull();
                created!.GenderId.ShouldBe(Gender.Unspecified);
                created.DateOfBirth.ShouldBe(DateTime.MinValue);
            }
        });
    }

    /// <summary>
    /// Firm name and firm email are attorney-only. A non-attorney form that carries a firm name
    /// must have it ignored, so the value is SEEDED on the request rather than left blank: an
    /// absent firm name would satisfy this assertion with the role gate deleted.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_NonAttorney_DoesNotPersistFirmNameOrFirmEmail()
    {
        var token = NewToken();
        var email = $"nfm-{token}@test.local";

        await _appService.RegisterAsync(new ExternalUserSignUpDto
        {
            UserType = ExternalUserType.Patient,
            Email = email,
            Password = Password,
            ConfirmPassword = Password,
            FirmName = $"TEST-Should-Be-Ignored-{token}",
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
        using (WithCurrentUser.Run(_principal, newUserId, IdentityUsersTestData.PatientRoleName))
        {
            var profile = await _appService.GetMyProfileAsync();
            profile.UserRole.ShouldBe(IdentityUsersTestData.PatientRoleName);
            profile.FirmName.ShouldBeEmpty();
        }
    }

    /// <summary>
    /// R2-4: a Claim Examiner is a full external party, so registration creates its master row.
    /// Without it the CE is invisible to linking and has no record to self-edit.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_ClaimExaminer_CreatesMasterAtRegistration()
    {
        var token = NewToken();
        var email = $"cex-{token}@test.local";

        await _appService.RegisterAsync(new ExternalUserSignUpDto
        {
            UserType = ExternalUserType.ClaimExaminer,
            Email = email,
            Password = Password,
            ConfirmPassword = Password,
            FirstName = "TEST-Claim",
            LastName = "TEST-Examiner",
            TenantId = TenantsTestData.TenantARef,
        });

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var account = await _userManager.FindByEmailAsync(email);
            account.ShouldNotBeNull();

            var masters = await _claimExaminerRepository.GetListAsync(
                c => c.Email != null && c.Email == email);
            masters.Count.ShouldBe(1);
            masters[0].IdentityUserId.ShouldBe(account!.Id);
        }
    }

    /// <summary>
    /// The duplicate-email gate is PER OFFICE. Two offices are two databases and two rosters, so
    /// the same person registering at a second office is a new account, not a duplicate.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_SameEmailInTwoOffices_IsNotADuplicate()
    {
        var token = NewToken();
        var email = $"two-{token}@test.local";

        await _appService.RegisterAsync(new ExternalUserSignUpDto
        {
            UserType = ExternalUserType.Patient,
            Email = email,
            Password = Password,
            ConfirmPassword = Password,
            TenantId = TenantsTestData.TenantARef,
        });

        // The second call is the assertion: it must not throw RegistrationDuplicateEmail.
        await _appService.RegisterAsync(new ExternalUserSignUpDto
        {
            UserType = ExternalUserType.Patient,
            Email = email,
            Password = Password,
            ConfirmPassword = Password,
            TenantId = TenantsTestData.TenantBRef,
        });

        Guid inOfficeA;
        Guid inOfficeB;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var accountA = await _userManager.FindByEmailAsync(email);
            accountA.ShouldNotBeNull();
            inOfficeA = accountA!.Id;
        }

        using (_currentTenant.Change(TenantsTestData.TenantBRef))
        {
            var accountB = await _userManager.FindByEmailAsync(email);
            accountB.ShouldNotBeNull();
            inOfficeB = accountB!.Id;
        }

        inOfficeA.ShouldNotBe(inOfficeB);
    }

    /// <summary>
    /// An external account always belongs to an office. With no ambient office and none named on
    /// the request, registration fails fast instead of creating a host-scoped orphan that no
    /// office portal can sign in.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_NoAmbientTenantAndNoTenantId_RequiresTenantSelection()
    {
        var token = NewToken();
        var email = $"nte-{token}@test.local";

        // No CurrentTenant.Change: the ambient scope is the host, and TenantId is null.
        var ex = await Should.ThrowAsync<UserFriendlyException>(
            () => _appService.RegisterAsync(new ExternalUserSignUpDto
            {
                UserType = ExternalUserType.Patient,
                Email = email,
                Password = Password,
                ConfirmPassword = Password,
                TenantId = null,
            }));

        ex.Message.ShouldContain("Tenant selection is required");

        // And nothing was created at host scope on the way to the throw.
        (await _userManager.FindByEmailAsync(email)).ShouldBeNull();
    }
}
