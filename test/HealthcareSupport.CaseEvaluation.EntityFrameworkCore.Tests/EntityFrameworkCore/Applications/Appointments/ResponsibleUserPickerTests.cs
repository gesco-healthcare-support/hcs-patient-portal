using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// The Responsible User on the real rig (2026-10-26). The Approve dialog's list
/// (<see cref="IAppointmentApprovalAppService.GetInternalUserLookupAsync"/>) and the approval itself apply ONE
/// rule (<see cref="ResponsibleUserCandidates"/>): a switched-on staff account of this office. Whoever is
/// stored receives the appointment's "you are responsible" mail and its user queries, so a switched-off
/// account -- such as an Intake operator's office login locked when they were unassigned, which keeps its
/// role -- must be neither offered nor accepted.
/// </summary>
public class ResponsibleUserPickerTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private static readonly Guid StaffUserId = new("9f5b3c4d-0000-4000-9000-0000000000a1");

    [Fact]
    public async Task ASwitchedOffStaffUser_IsNotOffered()
    {
        var active = await SeedAsync(TenantsTestData.TenantARef, "Intake Staff", "picker-on", switchedOn: true);
        var switchedOff = await SeedAsync(TenantsTestData.TenantARef, "Intake Staff", "picker-off", switchedOn: false);

        PagedResultDto<LookupDto<Guid>> offered = null!;
        await AsStaffInOfficeAAsync(async () => offered = await GetRequiredService<IAppointmentApprovalAppService>()
            .GetInternalUserLookupAsync(new LookupRequestDto { Filter = "TEST-picker-", MaxResultCount = 50 }));

        var ids = offered.Items.Select(i => i.Id).ToList();
        ids.ShouldContain(active);
        ids.ShouldNotContain(switchedOff);
        offered.TotalCount.ShouldBe(ids.Count);
    }

    [Fact]
    public async Task ApprovingWithASwitchedOffStaffUser_IsRefused_AndNothingIsStored()
    {
        var switchedOff = await SeedAsync(TenantsTestData.TenantARef, "Intake Staff", "approve-off", switchedOn: false);

        await ShouldRefuseAsync(switchedOff);
    }

    [Fact]
    public async Task ApprovingWithAnAccountThatIsNotStaff_IsRefused()
    {
        var patient = await SeedAsync(TenantsTestData.TenantARef, "Patient", "approve-patient", switchedOn: true);

        await ShouldRefuseAsync(patient);
    }

    [Fact]
    public async Task ApprovingWithAnotherOfficesStaffUser_OrAnUnknownId_IsRefused()
    {
        var otherOffice = await SeedAsync(TenantsTestData.TenantBRef, "Intake Staff", "approve-office-b", switchedOn: true);

        await ShouldRefuseAsync(otherOffice);
        await ShouldRefuseAsync(Guid.NewGuid());
    }

    [Fact]
    public async Task ASwitchedOnStaffUserOfTheOffice_IsAccepted()
    {
        // Positive control for the refusals above: the same rule, the same office, a switched-on account.
        var active = await SeedAsync(TenantsTestData.TenantARef, "Intake Staff", "approve-on", switchedOn: true);

        await AsStaffInOfficeAAsync(() => GetRequiredService<ResponsibleUserCandidates>().EnsureEligibleAsync(active));
    }

    private async Task ShouldRefuseAsync(Guid responsibleUserId)
    {
        await AsStaffInOfficeAAsync(async () =>
            (await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IAppointmentApprovalAppService>()
                .ApproveAppointmentAsync(AppointmentsTestData.Appointment1Id,
                    new ApproveAppointmentInput { PrimaryResponsibleUserId = responsibleUserId })))
            .Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentResponsibleUserNotEligible));

        var stored = await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                return await GetRequiredService<IRepository<Appointment, Guid>>().GetAsync(AppointmentsTestData.Appointment1Id);
            }
        });
        stored.AppointmentStatus.ShouldBe(AppointmentStatusType.Pending);
        stored.PrimaryResponsibleUserId.ShouldNotBe((Guid?)responsibleUserId);
    }

    private Task AsStaffInOfficeAAsync(Func<Task> body) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.Run(GetRequiredService<ICurrentPrincipalAccessor>(), StaffUserId, "Staff Supervisor"))
        {
            await body();
        }
    });

    private async Task<Guid> SeedAsync(Guid office, string role, string label, bool switchedOn)
    {
        var id = Guid.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(office))
            {
                var users = GetRequiredService<IdentityUserManager>();
                var roles = GetRequiredService<IdentityRoleManager>();
                var email = switchedOn
                    ? await RoleUserSeeder.CreateUserInRoleAsync(users, roles, office, role, label)
                    : await RoleUserSeeder.CreateInactiveUserInRoleAsync(users, roles, office, role, label);
                id = (await users.FindByEmailAsync(email))!.Id;
            }
        });
        return id;
    }
}
