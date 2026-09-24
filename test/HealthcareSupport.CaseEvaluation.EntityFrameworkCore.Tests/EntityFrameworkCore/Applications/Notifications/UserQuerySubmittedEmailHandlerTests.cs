using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications;

/// <summary>
/// <see cref="UserQuerySubmittedEmailHandler"/> on the real rig. A query that names an APPROVED
/// appointment goes to that appointment's responsible user; anything else goes to every host
/// IT Admin. The IT-Admin Facts seed a host user in ANOTHER role as a decoy, so "only IT Admins"
/// is shown against someone who could have been wrongly included.
/// </summary>
public class UserQuerySubmittedEmailHandlerTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private const string Message = "TEST- a question from the portal";

    private readonly RecordingNotificationDispatcher _dispatcher = new();

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<INotificationDispatcher>(_dispatcher));
    }

    [Fact]
    public async Task ApprovedAppointmentWithAResponsibleUser_EmailsThatUserOnly()
    {
        // Appointment2 is seeded Approved in office B; Patient2 is an office-B user with an email.
        await ChangeAppointment2Async(a => a.PrimaryResponsibleUserId = IdentityUsersTestData.Patient2UserId);
        var admins = await SeedItAdminsAsync();

        await RaiseAsync(AppointmentsTestData.Appointment2RequestConfirmationNumber, TenantsTestData.TenantBRef);

        var sent = _dispatcher.Dispatches.ShouldHaveSingleItem();
        sent.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.UserQuery);
        sent.Recipients.ShouldBe(new[] { IdentityUsersTestData.Patient2Email });
        sent.Recipients.ShouldNotContain(admins.Admin);
        sent.Variables["UserQueryMessage"].ShouldBe(Message);
        sent.Variables["UserQuerySubjectIdentity"].ShouldBeOfType<string>().ShouldStartWith(" - ");
    }

    [Fact]
    public async Task NoConfirmationNumber_EmailsEveryHostItAdmin_AndNotTheOtherRole()
    {
        var admins = await SeedItAdminsAsync();

        await RaiseAsync(confirmationNumber: null, TenantsTestData.TenantARef);

        var sent = _dispatcher.Dispatches.ShouldHaveSingleItem();
        sent.Recipients.ShouldContain(admins.Admin);
        sent.Recipients.ShouldNotContain(admins.Decoy);
        sent.Variables["UserQuerySubjectIdentity"].ShouldBe(string.Empty);
    }

    [Fact]
    public async Task ConfirmationNumberOfANonApprovedAppointment_FallsBackToTheItAdmins()
    {
        // Appointment1 is seeded Pending in office A: its responsible user must NOT be used.
        await ChangeAppointment1Async(a => a.PrimaryResponsibleUserId = IdentityUsersTestData.TenantAdmin1UserId);
        var admins = await SeedItAdminsAsync();

        await RaiseAsync(AppointmentsTestData.Appointment1RequestConfirmationNumber, TenantsTestData.TenantARef);

        var sent = _dispatcher.Dispatches.ShouldHaveSingleItem();
        sent.Recipients.ShouldContain(admins.Admin);
        sent.Recipients.ShouldNotContain(IdentityUsersTestData.TenantAdmin1Email);
    }

    [Fact]
    public async Task ApprovedAppointmentWithNoResponsibleUser_FallsBackToTheItAdmins()
    {
        await ChangeAppointment2Async(a => a.PrimaryResponsibleUserId = null);
        var admins = await SeedItAdminsAsync();

        await RaiseAsync(AppointmentsTestData.Appointment2RequestConfirmationNumber, TenantsTestData.TenantBRef);

        _dispatcher.Dispatches.ShouldHaveSingleItem().Recipients.ShouldContain(admins.Admin);
    }

    [Fact]
    public async Task NoItAdminAnywhere_SendsNothing()
    {
        // Positive control for this path: NoConfirmationNumber_EmailsEveryHostItAdmin_... above,
        // identical except that it seeds an IT Admin.
        await RaiseAsync(confirmationNumber: null, TenantsTestData.TenantARef);

        _dispatcher.Dispatches.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyMessage_SendsNothing(string message)
    {
        await SeedItAdminsAsync();

        await RaiseAsync(confirmationNumber: null, TenantsTestData.TenantARef, message);

        _dispatcher.Dispatches.ShouldBeEmpty();
    }

    [Fact]
    public async Task MissingTemplate_IsLoggedAndSwallowed_NotThrown()
    {
        await SeedItAdminsAsync();
        _dispatcher.ThrowTemplateNotFound = true;

        await Should.NotThrowAsync(() => RaiseAsync(confirmationNumber: null, TenantsTestData.TenantARef));

        _dispatcher.Dispatches.ShouldBeEmpty();
    }

    // ------------------------------------------------------------------------

    private async Task RaiseAsync(string? confirmationNumber, Guid tenantId, string message = Message)
    {
        _dispatcher.Clear();
        await WithUnitOfWorkAsync(() => GetRequiredService<UserQuerySubmittedEmailHandler>().HandleEventAsync(
            new UserQuerySubmittedEto
            {
                UserQueryId = Guid.NewGuid(),
                Message = message,
                RequestConfirmationNumber = confirmationNumber,
                TenantId = tenantId,
                OccurredAt = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc),
            }));
    }

    /// <summary>A host IT Admin, plus a host user in another role as the decoy.</summary>
    private async Task<(string Admin, string Decoy)> SeedItAdminsAsync()
    {
        string admin = string.Empty, decoy = string.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(null))
            {
                var users = GetRequiredService<IdentityUserManager>();
                var roles = GetRequiredService<IdentityRoleManager>();
                admin = await RoleUserSeeder.CreateUserInRoleAsync(
                    users, roles, null, InternalUserRoleDataSeedContributor.ItAdminRoleName, "query-itadmin");
                decoy = await RoleUserSeeder.CreateUserInRoleAsync(
                    users, roles, null, "TEST-host-other-role", "query-decoy");
            }
        });
        return (admin, decoy);
    }

    private Task ChangeAppointment1Async(Action<Appointment> change) =>
        ChangeAppointmentAsync(TenantsTestData.TenantARef, AppointmentsTestData.Appointment1Id, change);

    private Task ChangeAppointment2Async(Action<Appointment> change) =>
        ChangeAppointmentAsync(TenantsTestData.TenantBRef, AppointmentsTestData.Appointment2Id, change);

    private Task ChangeAppointmentAsync(Guid tenantId, Guid appointmentId, Action<Appointment> change) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(tenantId))
            {
                var repository = GetRequiredService<IRepository<Appointment, Guid>>();
                var appointment = await repository.GetAsync(appointmentId);
                change(appointment);
                await repository.UpdateAsync(appointment, autoSave: true);
            }
        });
}
