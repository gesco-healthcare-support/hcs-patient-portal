using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications;

/// <summary>
/// <see cref="AppNotificationManager.RaiseForOfficeStaffAsync"/> on the real rig: the in-app bell goes to the
/// office's Staff Supervisor and Intake Staff users, one row each -- and (2026-10-26) not to a switched-off
/// account, such as an Intake operator's office shadow login locked when they were unassigned or moved to Staff
/// Supervisor. Its role stays, so a lookup by role alone still finds it.
/// </summary>
public class AppNotificationStaffRecipientsTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    [Fact]
    public async Task OnlyActiveStaffGetTheNotice()
    {
        var office = TenantsTestData.TenantARef;
        string supervisor = string.Empty, intake = string.Empty, switchedOff = string.Empty, decoy = string.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(office))
            {
                var users = GetRequiredService<IdentityUserManager>();
                var roles = GetRequiredService<IdentityRoleManager>();
                supervisor = await RoleUserSeeder.CreateUserInRoleAsync(users, roles, office, "Staff Supervisor", "bell-supervisor");
                intake = await RoleUserSeeder.CreateUserInRoleAsync(users, roles, office, "Intake Staff", "bell-intake");
                switchedOff = await RoleUserSeeder.CreateInactiveUserInRoleAsync(users, roles, office, "Intake Staff", "bell-off");
                decoy = await RoleUserSeeder.CreateUserInRoleAsync(users, roles, office, "TEST-other-role", "bell-decoy");
            }
        });

        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(office))
            {
                await GetRequiredService<AppNotificationManager>().RaiseForOfficeStaffAsync(
                    AppNotificationType.AppointmentRequested, "TEST-title", "TEST-body");
            }
        });

        var notified = await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(office))
            {
                var users = GetRequiredService<IdentityUserManager>();
                var ids = (await GetRequiredService<IRepository<AppNotification, System.Guid>>().GetListAsync(n => n.Title == "TEST-title"))
                    .Select(n => n.RecipientUserId)
                    .ToList();
                var emails = new System.Collections.Generic.List<string>();
                foreach (var id in ids)
                {
                    emails.Add((await users.GetByIdAsync(id)).Email!);
                }

                return emails;
            }
        });

        notified.ShouldContain(supervisor);
        notified.ShouldContain(intake);
        notified.ShouldNotContain(switchedOff);
        notified.ShouldNotContain(decoy);
        notified.Count.ShouldBe(notified.Distinct().Count());
    }
}
