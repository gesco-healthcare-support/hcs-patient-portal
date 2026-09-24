using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications.Delivery;

/// <summary>
/// <see cref="AppointmentRecipientResolver"/> on the real rig, for the paths no other test reaches:
/// an applicant-attorney link that names a registered user, the same user linked twice, and an
/// appointment that does not exist. The attorney's address can also arrive through a later pass
/// (the appointment's own attorney-email column), so these Facts assert WHICH pass produced the
/// entry, via its context, rather than merely that the address appears. Office B's Appointment2 is the
/// office decoy, resolved from both offices.
/// </summary>
public class AppointmentRecipientResolverTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    [Fact]
    public async Task TheSeededLinksUser_IsAddedByTheLinkPass_StampedWithTheOffice()
    {
        // The seeded join row (Join1) names ApplicantAttorney1.
        var recipients = await ResolveAsync(AppointmentsTestData.Appointment1Id);

        var attorney = recipients.Where(r => r.To == IdentityUsersTestData.ApplicantAttorney1Email).ShouldHaveSingleItem();
        attorney.Context.ShouldEndWith($"/aa/{AppointmentApplicantAttorneysTestData.Join1Id}");
        attorney.Role.ShouldBe(RecipientRole.ApplicantAttorney);
        attorney.TenantId.ShouldBe(TenantsTestData.TenantARef);
    }

    [Fact]
    public async Task ASecondLinkToTheSameUser_IsCollapsedIntoOneRecipient()
    {
        var secondLinkId = Guid.NewGuid();
        await InOfficeAAsync(async () =>
        {
            await GetRequiredService<IRepository<AppointmentApplicantAttorney, Guid>>().InsertAsync(
                new AppointmentApplicantAttorney(
                    secondLinkId,
                    AppointmentsTestData.Appointment1Id,
                    ApplicantAttorneysTestData.Attorney1Id,
                    IdentityUsersTestData.ApplicantAttorney1UserId),
                autoSave: true);
        });

        var recipients = await ResolveAsync(AppointmentsTestData.Appointment1Id);

        // Two link rows name the same user; the resolver dedups on address, so there is ONE recipient,
        // still attributed to a link pass. (Which link wins depends on row order, which the query
        // does not fix, so it is deliberately not asserted.)
        var attorney = recipients.Where(r => r.To == IdentityUsersTestData.ApplicantAttorney1Email).ShouldHaveSingleItem();
        attorney.Context.ShouldContain("/aa/");
    }

    [Fact]
    public async Task UnknownAppointment_HasNoRecipients()
    {
        var recipients = await ResolveAsync(Guid.NewGuid());

        recipients.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnotherOfficesAppointment_HasNoRecipientsFromThisOffice()
    {
        // The OFFICE decoy: Appointment2 is real, in office B, with Patient2 on it. Resolved from office
        // A it must yield nobody, or office A's notice would go to office B's parties.
        var recipients = await ResolveAsync(AppointmentsTestData.Appointment2Id);

        recipients.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheSameAppointment_HasRecipientsFromItsOwnOffice()
    {
        // Positive control for the Fact above: same appointment, its own office.
        var recipients = await ResolveAsync(AppointmentsTestData.Appointment2Id, TenantsTestData.TenantBRef);

        recipients.ShouldNotBeEmpty();
        recipients.ShouldAllBe(r => r.TenantId == TenantsTestData.TenantBRef);
    }

    // ------------------------------------------------------------------------

    private async Task<List<SendAppointmentEmailArgs>> ResolveAsync(Guid appointmentId, Guid? officeId = null)
    {
        List<SendAppointmentEmailArgs> result = new();
        await InOfficeAsync(officeId ?? TenantsTestData.TenantARef, async () =>
        {
            result = await GetRequiredService<IAppointmentRecipientResolver>()
                .ResolveAsync(appointmentId, NotificationKind.Approved);
        });
        return result;
    }

    private Task InOfficeAAsync(Func<Task> action) => InOfficeAsync(TenantsTestData.TenantARef, action);

    private Task InOfficeAsync(Guid officeId, Func<Task> action) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(officeId))
        {
            await action();
        }
    });
}
