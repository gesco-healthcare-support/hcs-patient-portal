using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
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

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications.Delivery;

/// <summary>
/// <see cref="AttyCEPacketEmailHandler"/> on the real rig. The attorney / claim-examiner packet goes
/// to applicant attorneys, defense attorneys and claim examiners ONLY, one email each with the
/// packet attached. The chosen parties always include decoys the handler must skip: a patient, a
/// blank address, and an attorney with no role. So "only these roles" is shown against people who
/// could have been included.
/// </summary>
public class AttyCEPacketEmailHandlerTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private const string DefenseEmail = "TEST-atty-defense@test.local";
    private const string ExaminerEmail = "TEST-atty-examiner@test.local";
    private const string PatientDecoy = "TEST-atty-patient-decoy@test.local";
    private const string RolelessDecoy = "TEST-atty-roleless-decoy@test.local";

    private readonly NotificationRecorder _recorder = new();
    private readonly ChosenRecipients _parties = new();

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<INotificationDispatcher>(_recorder));
        services.Replace(ServiceDescriptor.Singleton<IAppointmentRecipientResolver>(_parties));
    }

    [Fact]
    public async Task SendsOneEmailPerAttorneyAndExaminer_WithThePacket_AndNoneToOtherParties()
    {
        WithPartiesAndDecoys();
        await NameApplicantAttorneyAsync("TEST-Ada", "TEST-Atty");
        var packetId = Guid.NewGuid();

        await RaiseAsync(PacketKind.AttorneyClaimExaminer, packetId);

        _recorder.Sent.Count.ShouldBe(3);
        _recorder.Sent.SelectMany(s => s.Recipients).ShouldBe(
            new[] { IdentityUsersTestData.ApplicantAttorney1Email, DefenseEmail, ExaminerEmail }, ignoreOrder: true);
        foreach (var sent in _recorder.Sent)
        {
            sent.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.AppointmentDocumentAddWithAttachment);
            sent.Recipients.Count.ShouldBe(1);
            sent.Packet.ShouldNotBeNull();
            sent.Packet!.Kind.ShouldBe(PacketKind.AttorneyClaimExaminer);
            sent.Packet.PacketId.ShouldBe(packetId);
            sent.Packet.AppointmentId.ShouldBe(AppointmentsTestData.Appointment1Id);
            sent.Variables["PacketLabel"].ShouldBe("Appointment Notice");
            sent.Variables["URL"].ShouldBeOfType<string>()
                .ShouldEndWith($"/appointments/view/{AppointmentsTestData.Appointment1Id:N}");
        }
    }

    [Fact]
    public async Task GreetsARegisteredRecipientByName_AndAnUnregisteredOneNeutrally()
    {
        WithPartiesAndDecoys();
        await NameApplicantAttorneyAsync("TEST-Ada", "TEST-Atty");

        await RaiseAsync(PacketKind.AttorneyClaimExaminer);

        GreetingFor(IdentityUsersTestData.ApplicantAttorney1Email).ShouldBe("Hello TEST-Ada TEST-Atty,");
        GreetingFor(DefenseEmail).ShouldBe("Hello,");
    }

    [Theory]
    [InlineData(PacketKind.Patient)]
    [InlineData(PacketKind.Doctor)]
    public async Task OtherPacketKinds_SendNothing(PacketKind kind)
    {
        // Positive control: the first Fact, same parties, the attorney/examiner kind.
        WithPartiesAndDecoys();

        await RaiseAsync(kind);

        _recorder.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task OnlyNonAttorneyParties_SendsNothing()
    {
        _parties.Parties.Add(new SendAppointmentEmailArgs { To = PatientDecoy, Role = RecipientRole.Patient });
        _parties.Parties.Add(new SendAppointmentEmailArgs { To = RolelessDecoy, Role = null });

        await RaiseAsync(PacketKind.AttorneyClaimExaminer);

        _recorder.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task UnknownAppointment_SendsNothing()
    {
        WithPartiesAndDecoys();

        await RaiseAsync(PacketKind.AttorneyClaimExaminer, appointmentId: Guid.NewGuid());

        _recorder.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task NullEvent_SendsNothing()
    {
        WithPartiesAndDecoys();
        _recorder.Clear();

        await WithUnitOfWorkAsync(() => GetRequiredService<AttyCEPacketEmailHandler>().HandleEventAsync(null!));

        _recorder.Sent.ShouldBeEmpty();
    }

    // ------------------------------------------------------------------------

    private string? GreetingFor(string email) =>
        _recorder.Sent.Single(s => s.Recipients.Contains(email)).Variables["Greeting"] as string;

    /// <summary>The three roles that should be emailed, plus the three decoys that should not.</summary>
    private void WithPartiesAndDecoys()
    {
        _parties.Parties.Add(new SendAppointmentEmailArgs { To = IdentityUsersTestData.ApplicantAttorney1Email, Role = RecipientRole.ApplicantAttorney });
        _parties.Parties.Add(new SendAppointmentEmailArgs { To = DefenseEmail, Role = RecipientRole.DefenseAttorney, IsRegistered = false });
        _parties.Parties.Add(new SendAppointmentEmailArgs { To = ExaminerEmail, Role = RecipientRole.ClaimExaminer, IsRegistered = false });
        _parties.Parties.Add(new SendAppointmentEmailArgs { To = PatientDecoy, Role = RecipientRole.Patient });
        _parties.Parties.Add(new SendAppointmentEmailArgs { To = " ", Role = RecipientRole.ApplicantAttorney });
        _parties.Parties.Add(new SendAppointmentEmailArgs { To = RolelessDecoy, Role = null });
    }

    private async Task RaiseAsync(PacketKind kind, Guid? packetId = null, Guid? appointmentId = null)
    {
        _recorder.Clear();
        await WithUnitOfWorkAsync(() => GetRequiredService<AttyCEPacketEmailHandler>().HandleEventAsync(new PacketGeneratedEto
        {
            AppointmentId = appointmentId ?? AppointmentsTestData.Appointment1Id,
            TenantId = TenantsTestData.TenantARef,
            PacketId = packetId ?? Guid.NewGuid(),
            Kind = kind,
            OccurredAt = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc),
        }));
    }

    private Task NameApplicantAttorneyAsync(string name, string surname) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
        {
            var repository = GetRequiredService<IRepository<IdentityUser, Guid>>();
            var user = await repository.GetAsync(IdentityUsersTestData.ApplicantAttorney1UserId);
            user.Name = name;
            user.Surname = surname;
            await repository.UpdateAsync(user, autoSave: true);
        }
    });
}
