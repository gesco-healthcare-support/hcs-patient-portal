using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications.Delivery;

/// <summary>
/// <see cref="AccessorInvitedEmailHandler"/> on the real rig: an invited accessor is emailed a
/// set-your-password link. Its guards (no event, an unknown appointment, an unknown invited user, and
/// a real user who belongs to another office) each send nothing, and each sits beside the sending
/// Fact on the same seeded data.
/// </summary>
public class AccessorInvitedEmailHandlerTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private const string InviteAddress = "TEST-invited-accessor@test.local";

    private readonly NotificationRecorder _recorder = new();

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<INotificationDispatcher>(_recorder));
    }

    [Fact]
    public async Task InvitedUser_IsEmailedTheSetupLink_AtTheInviteAddress()
    {
        await RaiseAsync(AppointmentsTestData.Appointment1Id, IdentityUsersTestData.ApplicantAttorney1UserId);

        var sent = _recorder.Sent.ShouldHaveSingleItem();
        sent.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.AccessorAppointmentBooked);
        sent.Recipients.ShouldBe(new[] { InviteAddress });
    }

    [Fact]
    public async Task UnknownAppointment_SendsNothing()
    {
        await RaiseAsync(Guid.NewGuid(), IdentityUsersTestData.ApplicantAttorney1UserId);

        _recorder.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task UnknownInvitedUser_SendsNothing()
    {
        await RaiseAsync(AppointmentsTestData.Appointment1Id, Guid.NewGuid());

        _recorder.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task InviteNamingAnotherOfficesUser_SendsNothing()
    {
        // The OFFICE decoy: Patient2 is a real user, in office B. An office-A invite naming them must
        // not find them, or office A's setup link would go to office B's account.
        await RaiseAsync(AppointmentsTestData.Appointment1Id, IdentityUsersTestData.Patient2UserId);

        _recorder.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task NullEvent_SendsNothing()
    {
        _recorder.Clear();

        await WithUnitOfWorkAsync(() => GetRequiredService<AccessorInvitedEmailHandler>().HandleEventAsync(null!));

        _recorder.Sent.ShouldBeEmpty();
    }

    // ------------------------------------------------------------------------

    private async Task RaiseAsync(Guid appointmentId, Guid invitedUserId)
    {
        _recorder.Clear();
        await WithUnitOfWorkAsync(() => GetRequiredService<AccessorInvitedEmailHandler>().HandleEventAsync(
            new AppointmentAccessorInvitedEto
            {
                AppointmentId = appointmentId,
                InvitedUserId = invitedUserId,
                TenantId = TenantsTestData.TenantARef,
                Email = InviteAddress,
                RoleName = "Defense Attorney",
                OccurredAt = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc),
            }));
    }
}
