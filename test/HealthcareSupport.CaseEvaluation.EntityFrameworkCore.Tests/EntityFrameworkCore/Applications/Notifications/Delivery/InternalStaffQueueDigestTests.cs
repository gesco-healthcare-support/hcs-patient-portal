using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using HealthcareSupport.CaseEvaluation.Notifications.Jobs;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications.Delivery;

/// <summary>
/// The daily internal-staff queue digest, end to end: <see cref="InternalStaffQueueDigestJob"/>
/// counts each office's pending and approved appointments and raises one event per Staff Supervisor
/// / Intake Staff user, and <see cref="InternalStaffQueueDigestEmailHandler"/> turns each into an
/// email. Only the dispatcher is replaced (it records), so the job, the real local event bus, the
/// real users and roles, and the real counts are all exercised.
///
/// <para>Office A is seeded with one Pending appointment and office B with one Approved. Staff are
/// seeded in office A with a decoy in another role, and one Fact also seeds the same two roles in
/// office B, so the counts, the office scoping and the role filter each have something to be wrong
/// about.</para>
/// </summary>
public class InternalStaffQueueDigestTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly NotificationRecorder _recorder = new();

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<INotificationDispatcher>(_recorder));
    }

    [Fact]
    public async Task EmailsEachOfficeAStaffUserOnce_WithOfficeACounts_AndNoOneElse()
    {
        var staff = await SeedOfficeAStaffAsync();

        await RunJobAsync();

        _recorder.Sent.Count.ShouldBe(2);
        _recorder.Sent.ShouldAllBe(s => s.TemplateCode == NotificationTemplateConsts.Codes.AppointmentApproveRejectInternal);
        _recorder.Sent.SelectMany(s => s.Recipients).ShouldBe(new[] { staff.Supervisor, staff.Intake }, ignoreOrder: true);
        _recorder.Sent.SelectMany(s => s.Recipients).ShouldNotContain(staff.Decoy);
        foreach (var sent in _recorder.Sent)
        {
            // Office A holds Appointment1 (Pending); office B's Approved appointment must not leak in.
            sent.Variables["PendingAppointmentCount"].ShouldBe(1);
            sent.Variables["ApprovedAppointmentCount"].ShouldBe(0);
        }
    }

    [Fact]
    public async Task EachOfficesStaff_GetTheirOwnOfficesCounts_Only()
    {
        // The OFFICE decoys: the same two roles in office B. Office B's staff are entitled to office
        // B's digest (0 pending, 1 approved) and must never receive office A's, and vice versa.
        var officeA = await SeedOfficeAStaffAsync();
        var officeB = await SeedOfficeBStaffAsync();

        await RunJobAsync();

        _recorder.Sent.Count.ShouldBe(4);
        foreach (var address in new[] { officeA.Supervisor, officeA.Intake })
        {
            var sent = _recorder.Sent.Where(s => s.Recipients.Contains(address)).ShouldHaveSingleItem();
            sent.Variables["PendingAppointmentCount"].ShouldBe(1);
            sent.Variables["ApprovedAppointmentCount"].ShouldBe(0);
        }

        foreach (var address in new[] { officeB.Supervisor, officeB.Intake })
        {
            var sent = _recorder.Sent.Where(s => s.Recipients.Contains(address)).ShouldHaveSingleItem();
            sent.Variables["PendingAppointmentCount"].ShouldBe(0);
            sent.Variables["ApprovedAppointmentCount"].ShouldBe(1);
        }
    }

    [Fact]
    public async Task GreetsAStaffUserByName_OrByUserNameWhenUnnamed()
    {
        var staff = await SeedOfficeAStaffAsync();
        await NameUserAsync(staff.Supervisor, "TEST-Sue");

        await RunJobAsync();

        FirstNameFor(staff.Supervisor).ShouldBe("TEST-Sue");
        FirstNameFor(staff.Intake).ShouldBe(staff.IntakeUserName);
    }

    [Fact]
    public async Task NoStaffInAnyOffice_SendsNothing()
    {
        // Positive control: the first Fact, identical except that it seeds staff.
        await RunJobAsync();

        _recorder.Sent.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DigestEmail_WithNoStaffAddress_SendsNothing(string email)
    {
        await RaiseDigestAsync(email);

        _recorder.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task DigestEmail_WithAStaffAddress_Sends()
    {
        await RaiseDigestAsync("TEST-digest-direct@test.local");

        _recorder.Sent.ShouldHaveSingleItem().Recipients.ShouldBe(new[] { "TEST-digest-direct@test.local" });
    }

    // ------------------------------------------------------------------------

    private string? FirstNameFor(string email) =>
        _recorder.Sent.Single(s => s.Recipients.Contains(email)).Variables["StaffFirstName"] as string;

    private async Task RunJobAsync()
    {
        _recorder.Clear();
        await WithUnitOfWorkAsync(() => GetRequiredService<InternalStaffQueueDigestJob>().ExecuteAsync());
    }

    private async Task RaiseDigestAsync(string email)
    {
        _recorder.Clear();
        await WithUnitOfWorkAsync(() => GetRequiredService<InternalStaffQueueDigestEmailHandler>().HandleEventAsync(
            new InternalStaffQueueDigestEto
            {
                TenantId = TenantsTestData.TenantARef,
                StaffUserId = Guid.NewGuid(),
                StaffEmail = email,
                StaffFirstName = "TEST-Direct",
                PendingAppointmentCount = 1,
                ApprovedAppointmentCount = 0,
                OccurredAt = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc),
            }));
    }

    /// <summary>A Supervisor who is ALSO Intake Staff (so the dedup matters), an Intake user, and a decoy.</summary>
    private async Task<(string Supervisor, string Intake, string IntakeUserName, string Decoy)> SeedOfficeAStaffAsync()
    {
        (string Email, string UserName) supervisor = ("", ""), intake = ("", ""), decoy = ("", "");
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                var users = GetRequiredService<IdentityUserManager>();
                var roles = GetRequiredService<IdentityRoleManager>();
                var office = TenantsTestData.TenantARef;
                supervisor = await StaffSeeder.CreateAsync(users, roles, office, "Staff Supervisor", "digest-supervisor");
                intake = await StaffSeeder.CreateAsync(users, roles, office, "Intake Staff", "digest-intake");
                decoy = await StaffSeeder.CreateAsync(users, roles, office, "TEST-other-role", "digest-decoy");
                (await users.AddToRoleAsync((await users.FindByEmailAsync(supervisor.Email))!, "Intake Staff")).Succeeded.ShouldBeTrue();
            }
        });
        return (supervisor.Email, intake.Email, intake.UserName, decoy.Email);
    }

    /// <summary>
    /// A Staff Supervisor and an Intake Staff user in office B, whose roles are created with ids that
    /// sort first (see <see cref="StaffSeeder.CreateRoleSortingFirstAsync"/>).
    /// </summary>
    private async Task<(string Supervisor, string Intake)> SeedOfficeBStaffAsync()
    {
        (string Email, string UserName) supervisor = ("", ""), intake = ("", "");
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantBRef))
            {
                var users = GetRequiredService<IdentityUserManager>();
                var roles = GetRequiredService<IdentityRoleManager>();
                var office = TenantsTestData.TenantBRef;
                await StaffSeeder.CreateRoleSortingFirstAsync(roles, office, "Staff Supervisor", 1);
                await StaffSeeder.CreateRoleSortingFirstAsync(roles, office, "Intake Staff", 2);
                supervisor = await StaffSeeder.CreateAsync(users, roles, office, "Staff Supervisor", "digest-officeb-supervisor");
                intake = await StaffSeeder.CreateAsync(users, roles, office, "Intake Staff", "digest-officeb-intake");
            }
        });
        return (supervisor.Email, intake.Email);
    }

    private Task NameUserAsync(string email, string name) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
        {
            var repository = GetRequiredService<IRepository<IdentityUser, Guid>>();
            var user = (await repository.GetListAsync(u => u.Email == email)).Single();
            user.Name = name;
            await repository.UpdateAsync(user, autoSave: true);
        }
    });
}
