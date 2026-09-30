using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.States;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Validation;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;

/// <summary>
/// The reads, lookups and required-id guards of the four services that attach people to an
/// appointment: applicant attorneys, appointment-applicant-attorney links, accessors and employer
/// details. Their existing tests cover the happy CRUD paths only.
/// </summary>
/// <remarks>
/// Every lookup runs with a non-matching row present, and the in-use delete runs against an
/// attorney that IS linked to an appointment (the seeded Join1). Office A, seeded data plus
/// synthetic rows.
/// </remarks>
public abstract class AppointmentPartyLinkServicesTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IApplicantAttorneysAppService _attorneys;
    private readonly IAppointmentApplicantAttorneysAppService _links;
    private readonly IAppointmentAccessorsAppService _accessors;
    private readonly IAppointmentEmployerDetailsAppService _employers;
    private readonly ICurrentTenant _currentTenant;

    protected AppointmentPartyLinkServicesTests()
    {
        _attorneys = GetRequiredService<IApplicantAttorneysAppService>();
        _links = GetRequiredService<IAppointmentApplicantAttorneysAppService>();
        _accessors = GetRequiredService<IAppointmentAccessorsAppService>();
        _employers = GetRequiredService<IAppointmentEmployerDetailsAppService>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private async Task<T> InOfficeA<T>(Func<Task<T>> call)
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            return await WithUnitOfWorkAsync(call);
        }
    }

    private async Task InOfficeA(Func<Task> call)
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await WithUnitOfWorkAsync(call);
        }
    }

    // ------------------------------------------------------------------ applicant attorneys

    [Fact]
    public async Task An_applicant_attorney_is_read_with_navigation_and_the_lookups_filter()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var userId = Guid.NewGuid();
        await InOfficeA(async () =>
        {
            var states = GetRequiredService<IRepository<State, Guid>>();
            await states.InsertAsync(new State(Guid.NewGuid(), $"Synthetic State {token}"), autoSave: true);
            await states.InsertAsync(new State(Guid.NewGuid(), "Synthetic Unmatched State"), autoSave: true);
            var user = new IdentityUser(userId, $"party-{token}", $"party-{token}@example.test", TenantsTestData.TenantARef);
            (await GetRequiredService<IdentityUserManager>().CreateAsync(user)).Succeeded.ShouldBeTrue();
        });

        var attorney = await InOfficeA(() => _attorneys.GetWithNavigationPropertiesAsync(ApplicantAttorneysTestData.Attorney1Id));
        var states = await InOfficeA(() => _attorneys.GetStateLookupAsync(new LookupRequestDto { Filter = token, MaxResultCount = 10 }));
        var users = await InOfficeA(() => _attorneys.GetIdentityUserLookupAsync(new LookupRequestDto { Filter = token, MaxResultCount = 10 }));
        var linkUsers = await InOfficeA(() => _links.GetIdentityUserLookupAsync(new LookupRequestDto { Filter = token, MaxResultCount = 10 }));

        attorney.ApplicantAttorney.Id.ShouldBe(ApplicantAttorneysTestData.Attorney1Id);
        states.Items.ShouldHaveSingleItem().DisplayName.ShouldBe($"Synthetic State {token}");
        users.Items.ShouldHaveSingleItem().Id.ShouldBe(userId);
        linkUsers.Items.ShouldHaveSingleItem().Id.ShouldBe(userId);
    }

    [Fact]
    public async Task An_applicant_attorney_linked_to_an_appointment_cannot_be_deleted()
    {
        var refused = await Should.ThrowAsync<BusinessException>(() =>
            InOfficeA(() => _attorneys.DeleteAsync(ApplicantAttorneysTestData.Attorney1Id)));

        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ApplicantAttorneyInUse);
        (await InOfficeA(() => GetRequiredService<IRepository<ApplicantAttorney, Guid>>().FindAsync(ApplicantAttorneysTestData.Attorney1Id)))
            .ShouldNotBeNull();
    }

    // ------------------------------------------------------------------ appointment-applicant-attorney links

    [Fact]
    public async Task A_link_is_read_with_navigation_and_needs_all_three_ids_to_be_created_or_changed()
    {
        var link = await InOfficeA(() => _links.GetWithNavigationPropertiesAsync(AppointmentApplicantAttorneysTestData.Join1Id));
        link.AppointmentApplicantAttorney.Id.ShouldBe(AppointmentApplicantAttorneysTestData.Join1Id);

        AppointmentApplicantAttorneyCreateDto Create(Guid appointment, Guid attorney, Guid user) =>
            new() { AppointmentId = appointment, ApplicantAttorneyId = attorney, IdentityUserId = user };
        AppointmentApplicantAttorneyUpdateDto Update(Guid appointment, Guid attorney, Guid user) =>
            new() { AppointmentId = appointment, ApplicantAttorneyId = attorney, IdentityUserId = user };
        var a = AppointmentsTestData.Appointment1Id;
        var at = ApplicantAttorneysTestData.Attorney1Id;
        var u = IdentityUsersTestData.ApplicantAttorney1UserId;

        foreach (var input in new[] { Create(Guid.Empty, at, u), Create(a, Guid.Empty, u), Create(a, at, Guid.Empty) })
        {
            await Should.ThrowAsync<UserFriendlyException>(() => InOfficeA(() => _links.CreateAsync(input)));
        }

        foreach (var input in new[] { Update(Guid.Empty, at, u), Update(a, Guid.Empty, u), Update(a, at, Guid.Empty) })
        {
            await Should.ThrowAsync<UserFriendlyException>(() =>
                InOfficeA(() => _links.UpdateAsync(AppointmentApplicantAttorneysTestData.Join1Id, input)));
        }

        (await InOfficeA(() => _links.GetAsync(AppointmentApplicantAttorneysTestData.Join1Id))).AppointmentId.ShouldBe(a);
    }

    // ------------------------------------------------------------------ accessors

    [Fact]
    public async Task An_accessor_is_read_with_navigation_and_needs_its_required_fields()
    {
        var accessor = await InOfficeA(() => _accessors.GetWithNavigationPropertiesAsync(AppointmentAccessorsTestData.Accessor1Id));
        accessor.AppointmentAccessor.Id.ShouldBe(AppointmentAccessorsTestData.Accessor1Id);

        var a = AppointmentsTestData.Appointment1Id;
        await Should.ThrowAsync<UserFriendlyException>(() => InOfficeA(() => _accessors.CreateAsync(
            new AppointmentAccessorCreateDto { AppointmentId = Guid.Empty, Email = "acc@example.test", Role = "Applicant Attorney" })));

        // A blank email or role never reaches the service's own guards: the DTO's [Required] refuses
        // it first, so those two in-method checks are unreachable through the service.
        foreach (var input in new[]
        {
            new AppointmentAccessorCreateDto { AppointmentId = a, Email = " ", Role = "Applicant Attorney" },
            new AppointmentAccessorCreateDto { AppointmentId = a, Email = "acc@example.test", Role = " " },
        })
        {
            await Should.ThrowAsync<AbpValidationException>(() => InOfficeA(() => _accessors.CreateAsync(input)));
        }

        foreach (var input in new[]
        {
            new AppointmentAccessorUpdateDto { IdentityUserId = Guid.Empty, AppointmentId = a },
            new AppointmentAccessorUpdateDto { IdentityUserId = IdentityUsersTestData.ApplicantAttorney1UserId, AppointmentId = Guid.Empty },
        })
        {
            await Should.ThrowAsync<UserFriendlyException>(() =>
                InOfficeA(() => _accessors.UpdateAsync(AppointmentAccessorsTestData.Accessor1Id, input)));
        }
    }

    // ------------------------------------------------------------------ employer details

    [Fact]
    public async Task The_employer_detail_appointment_lookup_filters_by_confirmation_number_and_an_update_needs_an_appointment()
    {
        var lookup = await InOfficeA(() => _employers.GetAppointmentLookupAsync(
            new LookupRequestDto { Filter = AppointmentsTestData.Appointment1RequestConfirmationNumber, MaxResultCount = 10 }));
        var none = await InOfficeA(() => _employers.GetAppointmentLookupAsync(
            new LookupRequestDto { Filter = "NO-SUCH-NUMBER", MaxResultCount = 10 }));

        lookup.Items.ShouldHaveSingleItem().Id.ShouldBe(AppointmentsTestData.Appointment1Id);
        none.TotalCount.ShouldBe(0);
        await Should.ThrowAsync<UserFriendlyException>(() => InOfficeA(() => _employers.UpdateAsync(
            Guid.NewGuid(), new AppointmentEmployerDetailUpdateDto { AppointmentId = Guid.Empty, EmployerName = "Synthetic Employer", Occupation = "Synthetic Role", ConcurrencyStamp = "synthetic-stamp" })));
    }
}
