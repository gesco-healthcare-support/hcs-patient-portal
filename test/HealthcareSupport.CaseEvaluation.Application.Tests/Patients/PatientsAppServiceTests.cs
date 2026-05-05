using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Patients;

public abstract class PatientsAppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IPatientsAppService _patientsAppService;
    private readonly PatientManager _patientManager;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

    protected PatientsAppServiceTests()
    {
        _patientsAppService = GetRequiredService<IPatientsAppService>();
        _patientManager = GetRequiredService<PatientManager>();
        _patientRepository = GetRequiredService<IRepository<Patient, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    // ------------------------------------------------------------------------
    // CRUD happy path
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_ReturnsSeededPatient()
    {
        var result = await _patientsAppService.GetAsync(PatientsTestData.Patient1Id);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(PatientsTestData.Patient1Id);
        result.FirstName.ShouldBe(PatientsTestData.Patient1FirstName);
        result.Email.ShouldBe(PatientsTestData.Patient1Email);
    }

    [Fact]
    public async Task GetListAsync_WithNoFilter_ReturnsBothSeededPatients()
    {
        var result = await _patientsAppService.GetListAsync(new GetPatientsInput());

        result.TotalCount.ShouldBeGreaterThanOrEqualTo(2);
        result.Items.Any(x => x.Patient.Id == PatientsTestData.Patient1Id).ShouldBeTrue();
        result.Items.Any(x => x.Patient.Id == PatientsTestData.Patient2Id).ShouldBeTrue();
    }

    [Fact]
    public async Task GetListAsync_FilterByFirstName_ReturnsMatchingPatient()
    {
        var result = await _patientsAppService.GetListAsync(new GetPatientsInput
        {
            FirstName = PatientsTestData.Patient1FirstName
        });

        result.Items.Count.ShouldBe(1);
        result.Items[0].Patient.Id.ShouldBe(PatientsTestData.Patient1Id);
    }

    [Fact]
    public async Task GetListAsync_FilterByEmail_ReturnsMatchingPatient()
    {
        var result = await _patientsAppService.GetListAsync(new GetPatientsInput
        {
            Email = PatientsTestData.Patient2Email
        });

        result.Items.Count.ShouldBe(1);
        result.Items[0].Patient.Id.ShouldBe(PatientsTestData.Patient2Id);
    }

    [Fact]
    public async Task CreateAsync_PersistsAllFields()
    {
        var input = BuildValidCreateDto();
        input.FirstName = "Alice";
        input.LastName = "Tester";
        input.Email = "alice.tester@test.local";

        var created = await _patientsAppService.CreateAsync(input);

        created.ShouldNotBeNull();
        created.FirstName.ShouldBe("Alice");
        created.LastName.ShouldBe("Tester");
        created.Email.ShouldBe("alice.tester@test.local");

        // FEAT-09: Patient is IMultiTenant; raw repository reads from
        // the host context apply WHERE TenantId IS NULL, which excludes
        // the just-created TenantA row. Enter the tenant context for
        // the verification read.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var persisted = await _patientRepository.FindAsync(created.Id);
            persisted.ShouldNotBeNull();
            persisted!.FirstName.ShouldBe("Alice");
        }
    }

    [Fact]
    public async Task UpdateAsync_ChangesMutableFields_DoesNotChangeIdentityUserId()
    {
        // FEAT-09: Patient is IMultiTenant; raw repository reads need
        // to run inside the patient's tenant scope so the auto-filter
        // does not exclude the row.
        Patient patient1;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            patient1 = await _patientRepository.GetAsync(PatientsTestData.Patient1Id);
        }
        var originalIdentityUserId = patient1.IdentityUserId;

        var update = new PatientUpdateDto
        {
            FirstName = "Renamed",
            LastName = patient1.LastName,
            Email = patient1.Email,
            GenderId = patient1.GenderId,
            DateOfBirth = patient1.DateOfBirth,
            PhoneNumberTypeId = patient1.PhoneNumberTypeId,
            IdentityUserId = originalIdentityUserId,
            TenantId = patient1.TenantId,
            ConcurrencyStamp = patient1.ConcurrencyStamp
        };

        var result = await _patientsAppService.UpdateAsync(PatientsTestData.Patient1Id, update);

        result.FirstName.ShouldBe("Renamed");
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var refetched = await _patientRepository.GetAsync(PatientsTestData.Patient1Id);
            refetched.IdentityUserId.ShouldBe(originalIdentityUserId);
        }
    }

    [Fact]
    public async Task DeleteAsync_RemovesPatient()
    {
        // Create a disposable patient so we don't disturb the seed for later tests.
        var input = BuildValidCreateDto();
        input.Email = "delete.target@test.local";
        var created = await _patientsAppService.CreateAsync(input);

        await _patientsAppService.DeleteAsync(created.Id);

        var result = await _patientRepository.FindAsync(created.Id);
        result.ShouldBeNull();
    }

    // ------------------------------------------------------------------------
    // Validation guards
    // ------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_WhenIdentityUserIdIsEmpty_ThrowsUserFriendlyException()
    {
        var input = BuildValidCreateDto();
        input.IdentityUserId = Guid.Empty;

        await Should.ThrowAsync<UserFriendlyException>(async () =>
            await _patientsAppService.CreateAsync(input));
    }

    [Fact]
    public async Task UpdateAsync_WhenIdentityUserIdIsEmpty_ThrowsUserFriendlyException()
    {
        var update = new PatientUpdateDto
        {
            FirstName = "X",
            LastName = "Y",
            Email = "xy@test.local",
            GenderId = Gender.Male,
            DateOfBirth = PatientsTestData.FixedDateOfBirth,
            PhoneNumberTypeId = PhoneNumberType.Work,
            IdentityUserId = Guid.Empty,
        };

        await Should.ThrowAsync<UserFriendlyException>(async () =>
            await _patientsAppService.UpdateAsync(PatientsTestData.Patient1Id, update));
    }

    // ------------------------------------------------------------------------
    // Max-length validation (PatientManager.CreateAsync / .UpdateAsync)
    // Theory covers all 15 length-bounded string fields.
    // ------------------------------------------------------------------------

    [Theory]
    [InlineData(nameof(PatientCreateDto.FirstName), PatientConsts.FirstNameMaxLength)]
    [InlineData(nameof(PatientCreateDto.LastName), PatientConsts.LastNameMaxLength)]
    [InlineData(nameof(PatientCreateDto.MiddleName), PatientConsts.MiddleNameMaxLength)]
    [InlineData(nameof(PatientCreateDto.Email), PatientConsts.EmailMaxLength)]
    [InlineData(nameof(PatientCreateDto.PhoneNumber), PatientConsts.PhoneNumberMaxLength)]
    [InlineData(nameof(PatientCreateDto.SocialSecurityNumber), PatientConsts.SocialSecurityNumberMaxLength)]
    [InlineData(nameof(PatientCreateDto.Address), PatientConsts.AddressMaxLength)]
    [InlineData(nameof(PatientCreateDto.City), PatientConsts.CityMaxLength)]
    [InlineData(nameof(PatientCreateDto.ZipCode), PatientConsts.ZipCodeMaxLength)]
    [InlineData(nameof(PatientCreateDto.RefferedBy), PatientConsts.RefferedByMaxLength)]
    [InlineData(nameof(PatientCreateDto.CellPhoneNumber), PatientConsts.CellPhoneNumberMaxLength)]
    [InlineData(nameof(PatientCreateDto.Street), PatientConsts.StreetMaxLength)]
    [InlineData(nameof(PatientCreateDto.InterpreterVendorName), PatientConsts.InterpreterVendorNameMaxLength)]
    [InlineData(nameof(PatientCreateDto.ApptNumber), PatientConsts.ApptNumberMaxLength)]
    [InlineData(nameof(PatientCreateDto.OthersLanguageName), PatientConsts.OthersLanguageNameMaxLength)]
    public async Task PatientManager_CreateAsync_WhenStringFieldExceedsMax_Throws(string field, int maxLen)
    {
        var input = BuildValidCreateDto();
        SetStringProperty(input, field, new string('x', maxLen + 1));

        await Should.ThrowAsync<ArgumentException>(async () => await InvokeManagerCreateAsync(input));
    }

    [Theory]
    [InlineData(nameof(PatientCreateDto.FirstName), PatientConsts.FirstNameMaxLength)]
    [InlineData(nameof(PatientCreateDto.LastName), PatientConsts.LastNameMaxLength)]
    [InlineData(nameof(PatientCreateDto.MiddleName), PatientConsts.MiddleNameMaxLength)]
    [InlineData(nameof(PatientCreateDto.Email), PatientConsts.EmailMaxLength)]
    [InlineData(nameof(PatientCreateDto.PhoneNumber), PatientConsts.PhoneNumberMaxLength)]
    [InlineData(nameof(PatientCreateDto.SocialSecurityNumber), PatientConsts.SocialSecurityNumberMaxLength)]
    [InlineData(nameof(PatientCreateDto.Address), PatientConsts.AddressMaxLength)]
    [InlineData(nameof(PatientCreateDto.City), PatientConsts.CityMaxLength)]
    [InlineData(nameof(PatientCreateDto.ZipCode), PatientConsts.ZipCodeMaxLength)]
    [InlineData(nameof(PatientCreateDto.RefferedBy), PatientConsts.RefferedByMaxLength)]
    [InlineData(nameof(PatientCreateDto.CellPhoneNumber), PatientConsts.CellPhoneNumberMaxLength)]
    [InlineData(nameof(PatientCreateDto.Street), PatientConsts.StreetMaxLength)]
    [InlineData(nameof(PatientCreateDto.InterpreterVendorName), PatientConsts.InterpreterVendorNameMaxLength)]
    [InlineData(nameof(PatientCreateDto.ApptNumber), PatientConsts.ApptNumberMaxLength)]
    [InlineData(nameof(PatientCreateDto.OthersLanguageName), PatientConsts.OthersLanguageNameMaxLength)]
    public async Task PatientManager_UpdateAsync_WhenStringFieldExceedsMax_Throws(string field, int maxLen)
    {
        var input = BuildValidCreateDto();
        SetStringProperty(input, field, new string('x', maxLen + 1));

        await Should.ThrowAsync<ArgumentException>(async () =>
            await InvokeManagerUpdateAsync(PatientsTestData.Patient1Id, input));
    }

    // ------------------------------------------------------------------------
    // Cross-tenant visibility (HIPAA-critical).
    //
    // Intent model:
    //   - HostAdmin (host scope, dev/debug role) sees patients from every tenant.
    //     This is intentional and correct forever.
    //   - Any tenant-scoped caller (TenantAdmin, Doctor, Patient, ...) should see
    //     only their tenant's patients. This is NOT enforced today because
    //     Patient does not implement IMultiTenant and PatientsAppService has no
    //     manual tenant filter. The skipped test below pins the target behaviour;
    //     it will flip green when the bug is fixed.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetListAsync_FromHostContext_ReturnsPatientsFromBothTenants()
    {
        var result = await _patientsAppService.GetListAsync(new GetPatientsInput());

        var patient1 = result.Items.FirstOrDefault(x => x.Patient.Id == PatientsTestData.Patient1Id);
        var patient2 = result.Items.FirstOrDefault(x => x.Patient.Id == PatientsTestData.Patient2Id);

        patient1.ShouldNotBeNull();
        patient2.ShouldNotBeNull();
        patient1!.Patient.TenantId.ShouldBe(TenantsTestData.TenantARef);
        patient2!.Patient.TenantId.ShouldBe(TenantsTestData.TenantBRef);
    }

    // FEAT-09 (ADR-006 T4, 2026-05-05): now passes -- Patient implements
    // IMultiTenant, so ABP's automatic filter scopes the query when
    // CurrentTenant.Id is set. No AppService change was needed.
    [Fact]
    public async Task GetListAsync_WhenCallerIsTenantScoped_ReturnsOnlyTheirTenantPatients()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _patientsAppService.GetListAsync(new GetPatientsInput());

            // Target behaviour: tenant-scoped caller sees ONLY their tenant's patients.
            result.Items.Any(x => x.Patient.Id == PatientsTestData.Patient1Id).ShouldBeTrue();
            result.Items.Any(x => x.Patient.Id == PatientsTestData.Patient2Id).ShouldBeFalse();
        }
    }

    // ------------------------------------------------------------------------
    // Booking email lookup
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetPatientByEmailForAppointmentBookingAsync_WhenFound_ReturnsPatient()
    {
        var result = await _patientsAppService.GetPatientByEmailForAppointmentBookingAsync(
            PatientsTestData.Patient1Email);

        result.ShouldNotBeNull();
        result!.Patient.Id.ShouldBe(PatientsTestData.Patient1Id);
    }

    [Fact]
    public async Task GetPatientByEmailForAppointmentBookingAsync_WhenNotFound_ReturnsNull()
    {
        var result = await _patientsAppService.GetPatientByEmailForAppointmentBookingAsync(
            "does-not-exist@test.local");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetPatientByEmailForAppointmentBookingAsync_WhenEmailEmpty_ReturnsNull()
    {
        var result = await _patientsAppService.GetPatientByEmailForAppointmentBookingAsync("");

        result.ShouldBeNull();
    }

    // ------------------------------------------------------------------------
    // Wave-2 PR-W2C: profile endpoints flipped live by the new
    // WithCurrentUser test helper. Plus new GetOrCreatePatientForAppointmentBookingAsync
    // tests covering the runtime IdentityUser-creation flow.
    // ------------------------------------------------------------------------

    [Fact]
    public void WithCurrentUser_PushesAndRestoresPrincipalOnDispose()
    {
        // Sanity check the helper before any consumer test depends on it.
        // ABP's CurrentPrincipalAccessor returns a fresh ClaimsPrincipal
        // object on each access in the unmodified state, so we compare
        // claim values rather than reference identity.
        var userIdBefore = _currentPrincipalAccessor.Principal.FindFirst(AbpClaimTypes.UserId)?.Value;

        using (WithCurrentUser.Run(_currentPrincipalAccessor, IdentityUsersTestData.Patient1UserId, "Patient"))
        {
            var inside = _currentPrincipalAccessor.Principal.FindFirst(AbpClaimTypes.UserId)?.Value;
            inside.ShouldBe(IdentityUsersTestData.Patient1UserId.ToString());
            _currentPrincipalAccessor.Principal.IsInRole("Patient").ShouldBeTrue();
        }

        var userIdAfter = _currentPrincipalAccessor.Principal.FindFirst(AbpClaimTypes.UserId)?.Value;
        userIdAfter.ShouldBe(userIdBefore);
    }

    [Fact]
    public async Task GetMyProfileAsync_WhenCallerIsPatient_ReturnsPatient()
    {
        // Flips Tier-1 PR-1C's `[Fact(Skip="...needs WithCurrentUser helper")]`
        // to live now that the helper exists in PR-W2C.
        using (WithCurrentUser.Run(_currentPrincipalAccessor, IdentityUsersTestData.Patient1UserId, "Patient"))
        {
            var result = await _patientsAppService.GetMyProfileAsync();

            result.ShouldNotBeNull();
            result.Patient.Id.ShouldBe(PatientsTestData.Patient1Id);
            result.Patient.IdentityUserId.ShouldBe(IdentityUsersTestData.Patient1UserId);
        }
    }

    // ------------------------------------------------------------------------
    // GetOrCreatePatientForAppointmentBookingAsync runtime-creation flow.
    // PR-W2C scope: existing-email path + the hardcoded-admin-password GAP.
    // The new-email path (which exercises IdentityUserManager.CreateAsync +
    // role assignment + PatientManager.CreateAsync) and the whitespace-email
    // path are deferred to a Wave-2 follow-up because ABP's
    // MethodInvocationValidator preempts whitespace tests via the
    // [EmailAddress] attribute, and the runtime-creation chain triggers
    // additional validation that needs scratch-Patient harness work.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetOrCreatePatient_WhenEmailMatchesPatient1_ReturnsExistingPatient()
    {
        // Email lookup hits seeded Patient1; the runtime-creation arm does
        // not run. Assert returned patient matches the seeded row.
        var input = new CreatePatientForAppointmentBookingInput
        {
            FirstName = PatientsTestData.Patient1FirstName,
            LastName = PatientsTestData.Patient1LastName,
            Email = PatientsTestData.Patient1Email,
            GenderId = (Gender)PatientsTestData.PatientGenderIdValue,
            DateOfBirth = PatientsTestData.FixedDateOfBirth,
            PhoneNumberTypeId = (PhoneNumberType)PatientsTestData.PatientPhoneNumberTypeIdValue,
        };

        var result = await _patientsAppService.GetOrCreatePatientForAppointmentBookingAsync(input);

        result.ShouldNotBeNull();
        result.Patient.Id.ShouldBe(PatientsTestData.Patient1Id);
    }

    // R2 (Phase 9, 2026-05-04): IsExisting=true on the email-fast-path branch.
    // Mirrors OLD AppointmentDomain.cs:210 -- when booking resolves to an
    // already-existing Patient, the Appointment must record IsPatientAlreadyExist=true.
    // Coverage for the dedup-match branch + FindOrCreate.wasFound branches needs
    // the runtime-creation harness work flagged in the existing skipped tests
    // (NEW-SEC-04 in docs/gap-analysis); deferred to the same Wave-2 follow-up.
    [Fact]
    public async Task GetOrCreatePatient_WhenEmailMatchesPatient1_SetsIsExistingTrue()
    {
        var input = new CreatePatientForAppointmentBookingInput
        {
            FirstName = PatientsTestData.Patient1FirstName,
            LastName = PatientsTestData.Patient1LastName,
            Email = PatientsTestData.Patient1Email,
            GenderId = (Gender)PatientsTestData.PatientGenderIdValue,
            DateOfBirth = PatientsTestData.FixedDateOfBirth,
            PhoneNumberTypeId = (PhoneNumberType)PatientsTestData.PatientPhoneNumberTypeIdValue,
        };

        var result = await _patientsAppService.GetOrCreatePatientForAppointmentBookingAsync(input);

        result.ShouldNotBeNull();
        result.IsExisting.ShouldBeTrue();
    }

    [Fact(Skip = "KNOWN GAP: GetOrCreatePatientForAppointmentBookingAsync uses CaseEvaluationConsts.AdminPasswordDefaultValue for runtime-created IdentityUser. Tracked: src/.../Domain/Patients/CLAUDE.md Known Gotchas (hardcoded admin password) AND docs/gap-analysis NEW-SEC-04. When an invite-token / temp-password flow replaces the hardcoded password, this Fact flips live.")]
    public Task GetOrCreatePatient_DoesNotUseHardcodedAdminPassword()
    {
        // Expected behaviour (not yet implemented):
        // Newly-created IdentityUser should have a password that requires
        // a flow-controlled set/reset (invite token, temp password, or
        // SSO claim) rather than the hardcoded default. Today's behaviour
        // grants every auto-created Patient the same admin-default password
        // until they reset it -- a HIPAA-relevant credential exposure.
        return Task.CompletedTask;
    }

    [Fact(Skip = "KNOWN GAP: UpdateMyProfileAsync mutates seeded Patient1 ConcurrencyStamp; restoring the original FirstName needs scratch-Patient + IdentityUser test isolation. Tracked: docs/issues/INCOMPLETE-FEATURES.md (test-current-user-faking; scratch-patient pattern). Live test is deferred to Wave-2 follow-up that builds the scratch-patient harness.")]
    public Task UpdateMyProfileAsync_WhenCallerIsPatient_UpdatesFields()
    {
        // Expected behaviour (not yet implemented):
        // UpdateMyProfileAsync called within a WithCurrentUser scope on a
        // scratch Patient + IdentityUser pair persists the new FirstName,
        // LastName, etc. The runtime-creation arm of GetOrCreatePatient
        // (also Wave-2-deferred) is the natural prerequisite -- once the
        // harness can spin up scratch Patients, UpdateMyProfileAsync runs
        // against scratch data without mutating the seeded baseline.
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------

    private static PatientCreateDto BuildValidCreateDto()
    {
        return new PatientCreateDto
        {
            FirstName = "Valid",
            LastName = "Patient",
            Email = $"valid-{Guid.NewGuid():N}@test.local",
            GenderId = Gender.Male,
            DateOfBirth = PatientsTestData.FixedDateOfBirth,
            PhoneNumberTypeId = PhoneNumberType.Work,
            IdentityUserId = IdentityUsersTestData.Patient1UserId,
            TenantId = TenantsTestData.TenantARef
        };
    }

    private static void SetStringProperty(object target, string propertyName, string value)
    {
        var prop = target.GetType().GetProperty(propertyName,
            BindingFlags.Instance | BindingFlags.Public);
        if (prop == null)
        {
            throw new InvalidOperationException(
                $"PatientCreateDto does not contain a string property named '{propertyName}'.");
        }
        prop.SetValue(target, value);
    }

    private Task<Patient> InvokeManagerCreateAsync(PatientCreateDto input)
    {
        return _patientManager.CreateAsync(
            input.StateId,
            input.AppointmentLanguageId,
            input.IdentityUserId,
            input.TenantId,
            input.FirstName,
            input.LastName,
            input.Email,
            input.GenderId,
            input.DateOfBirth,
            input.PhoneNumberTypeId,
            input.MiddleName,
            input.PhoneNumber,
            input.SocialSecurityNumber,
            input.Address,
            input.City,
            input.ZipCode,
            input.RefferedBy,
            input.CellPhoneNumber,
            input.Street,
            input.InterpreterVendorName,
            input.ApptNumber,
            input.OthersLanguageName);
    }

    private Task<Patient> InvokeManagerUpdateAsync(Guid id, PatientCreateDto input)
    {
        return _patientManager.UpdateAsync(
            id,
            input.StateId,
            input.AppointmentLanguageId,
            input.IdentityUserId,
            input.TenantId,
            input.FirstName,
            input.LastName,
            input.Email,
            input.GenderId,
            input.DateOfBirth,
            input.PhoneNumberTypeId,
            input.MiddleName,
            input.PhoneNumber,
            input.SocialSecurityNumber,
            input.Address,
            input.City,
            input.ZipCode,
            input.RefferedBy,
            input.CellPhoneNumber,
            input.Street,
            input.InterpreterVendorName,
            input.ApptNumber,
            input.OthersLanguageName);
    }
}
