using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Volo.Abp;
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
/// could have been included. Two Facts add OFFICE decoys: a named user who exists only in office B,
/// and office B's appointment raised from office A.
/// </summary>
public class AttyCEPacketEmailHandlerTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private const string DefenseEmail = "TEST-atty-defense@test.local";
    private const string ExaminerEmail = "TEST-atty-examiner@test.local";
    private const string PatientDecoy = "TEST-atty-patient-decoy@test.local";
    private const string RolelessDecoy = "TEST-atty-roleless-decoy@test.local";
    private const string OfficeBExaminerEmail = "TEST-atty-officeb-examiner@test.local";

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

    [Fact]
    public async Task AnotherOfficesNamedUser_IsGreetedNeutrally_NotByName()
    {
        // The OFFICE decoy for the name lookup: the examiner's address belongs to a NAMED user in office
        // B only. Office A's email must not find them, so it greets neutrally; greeting them by name
        // would show that the address has an account in another office.
        WithPartiesAndDecoys();
        await NameApplicantAttorneyAsync("TEST-Ada", "TEST-Atty");
        await CreateNamedOfficeBUserAsync(OfficeBExaminerEmail, "TEST-Bea", "TEST-Other");
        _parties.Parties.Add(new SendAppointmentEmailArgs { To = OfficeBExaminerEmail, Role = RecipientRole.ClaimExaminer, IsRegistered = false });

        await RaiseAsync(PacketKind.AttorneyClaimExaminer);

        GreetingFor(OfficeBExaminerEmail).ShouldBe("Hello,");
        GreetingFor(IdentityUsersTestData.ApplicantAttorney1Email).ShouldBe("Hello TEST-Ada TEST-Atty,");
    }

    [Fact]
    public async Task PacketRaisedFromThisOffice_ForAnotherOfficesAppointment_SendsNothing()
    {
        // The OFFICE decoy for the appointment lookup: Appointment2 is real, in office B. Positive
        // control: the first Fact, the same parties on office A's own appointment.
        WithPartiesAndDecoys();

        await RaiseAsync(PacketKind.AttorneyClaimExaminer, appointmentId: AppointmentsTestData.Appointment2Id);

        _recorder.Sent.ShouldBeEmpty();
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

    // ---- 2026-11-03: the booker (the account that submitted the appointment) is mailed too ----

    [Fact]
    public async Task AnAttorneyBooker_WhoseAddressIsNotTheTypedAttorney_IsMailedWithTheParties_OneEmailEach()
    {
        // The A00002 shape: a firm login submitted the request; the attorney typed on it is someone else.
        WithPartiesAndDecoys();
        var bookerEmail = await CreateBookerAsync("Applicant Attorney");

        await RaiseAsync(PacketKind.AttorneyClaimExaminer);

        _recorder.Sent.Count.ShouldBe(4);
        _recorder.Sent.SelectMany(s => s.Recipients).ShouldBe(
            new[] { IdentityUsersTestData.ApplicantAttorney1Email, DefenseEmail, ExaminerEmail, bookerEmail }, ignoreOrder: true);
        var bookerMail = _recorder.Sent.Single(s => s.Recipients.Contains(bookerEmail));
        bookerMail.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.AppointmentDocumentAddWithAttachment);
        bookerMail.Packet!.Kind.ShouldBe(PacketKind.AttorneyClaimExaminer);
        bookerMail.Variables["PacketLabel"].ShouldBe("Appointment Notice");
    }

    [Fact]
    public async Task ABookerWhoIsAlsoTheTypedAttorney_IsMailedOnce_WhateverTheCase()
    {
        _parties.Parties.Add(new SendAppointmentEmailArgs { To = IdentityUsersTestData.ApplicantAttorney1Email.ToLowerInvariant(), Role = RecipientRole.ApplicantAttorney });
        await SetSubmitterAsync(IdentityUsersTestData.ApplicantAttorney1UserId);

        await RaiseAsync(PacketKind.AttorneyClaimExaminer);

        _recorder.Sent.Count.ShouldBe(1);
        _recorder.Sent.Single().Recipients.Single().ShouldBe(
            IdentityUsersTestData.ApplicantAttorney1Email, StringCompareShould.IgnoreCase);
    }

    [Fact]
    public async Task AnAttorneyBooker_WhoIsThePatientsOwnLogin_IsNotMailedTheAttorneyPacket()
    {
        // Positive control: the first Fact in this group, the same booker role when the booker is not the patient.
        WithPartiesAndDecoys();
        var bookerEmail = await CreateBookerAsync("Applicant Attorney");
        await SetPatientLoginAsync(bookerEmail);

        await RaiseAsync(PacketKind.AttorneyClaimExaminer);

        _recorder.Sent.SelectMany(s => s.Recipients).ShouldNotContain(bookerEmail);
        _recorder.Sent.Count.ShouldBe(3);
    }

    [Fact]
    public async Task AnAttorneyBooker_WhoseConfirmedEmailIsThePatientsAddress_IsNotMailedTheAttorneyPacket()
    {
        WithPartiesAndDecoys();
        var bookerEmail = await CreateBookerAsync("Applicant Attorney", email: PatientsTestData.Patient1Email, confirmed: true);

        await RaiseAsync(PacketKind.AttorneyClaimExaminer);

        _recorder.Sent.SelectMany(s => s.Recipients).ShouldNotContain(bookerEmail);
        _recorder.Sent.Count.ShouldBe(3);
    }

    [Theory]
    [InlineData("Intake Staff")]
    [InlineData("Staff Supervisor")]
    public async Task AStaffBooker_IsNotMailedTheAttorneyPacket(string staffRole)
    {
        WithPartiesAndDecoys();
        var bookerEmail = await CreateBookerAsync(staffRole);

        await RaiseAsync(PacketKind.AttorneyClaimExaminer);

        _recorder.Sent.SelectMany(s => s.Recipients).ShouldNotContain(bookerEmail);
        _recorder.Sent.Count.ShouldBe(3);
    }

    [Fact]
    public async Task AnInactiveBooker_IsNotMailedTheAttorneyPacket()
    {
        WithPartiesAndDecoys();
        var bookerEmail = await CreateBookerAsync("Applicant Attorney", active: false);

        await RaiseAsync(PacketKind.AttorneyClaimExaminer);

        _recorder.Sent.SelectMany(s => s.Recipients).ShouldNotContain(bookerEmail);
        _recorder.Sent.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ABookerWhoHoldsPatientAndAttorneyRoles_FailsPacketVisibility_AndIsNotMailed()
    {
        // Any account holding the Patient role may see only the Patient packet, whatever else it holds.
        WithPartiesAndDecoys();
        var bookerEmail = await CreateBookerAsync("Applicant Attorney", extraRole: "Patient");

        await RaiseAsync(PacketKind.AttorneyClaimExaminer);

        _recorder.Sent.SelectMany(s => s.Recipients).ShouldNotContain(bookerEmail);
        _recorder.Sent.Count.ShouldBe(3);
    }

    [Fact]
    public async Task AnAttorneyBookerFromAnotherOffice_IsNotMailed()
    {
        // The submitter id resolves in office A only; an office B account with that role is never found.
        WithPartiesAndDecoys();
        var otherOfficeId = Guid.NewGuid();
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantBRef))
            {
                var users = GetRequiredService<IdentityUserManager>();
                var email = await RoleUserSeeder.CreateUserInRoleAsync(
                    users, GetRequiredService<IdentityRoleManager>(), TenantsTestData.TenantBRef, "Applicant Attorney", "atty-officeb-booker");
                otherOfficeId = (await users.FindByEmailAsync(email))!.Id;
            }
        });
        await SetSubmitterAsync(otherOfficeId);

        await RaiseAsync(PacketKind.AttorneyClaimExaminer);

        _recorder.Sent.Count.ShouldBe(3);
    }

    // ------------------------------------------------------------------------

    /// <summary>Creates a booker in office A, records them as the appointment's submitter, returns their address.</summary>
    private async Task<string> CreateBookerAsync(string role, bool active = true, string? email = null, bool confirmed = false, string? extraRole = null)
    {
        var id = Guid.Empty;
        var address = string.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                var users = GetRequiredService<IdentityUserManager>();
                var roles = GetRequiredService<IdentityRoleManager>();
                address = await RoleUserSeeder.CreateUserInRoleAsync(users, roles, TenantsTestData.TenantARef, role, "atty-booker");
                var user = (await users.FindByEmailAsync(address))!;
                if (email != null)
                {
                    (await users.SetEmailAsync(user, email)).Succeeded.ShouldBeTrue();
                    address = email;
                }
                user.SetEmailConfirmed(confirmed);
                user.SetIsActive(active);
                (await users.UpdateAsync(user)).Succeeded.ShouldBeTrue();
                if (extraRole != null)
                {
                    if (await roles.FindByNameAsync(extraRole) == null)
                    {
                        (await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), extraRole, TenantsTestData.TenantARef))).Succeeded.ShouldBeTrue();
                    }
                    (await users.AddToRoleAsync(user, extraRole)).Succeeded.ShouldBeTrue();
                }
                id = user.Id;
            }
        });
        await SetSubmitterAsync(id);
        return address;
    }

    private Task SetSubmitterAsync(Guid creator) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
        {
            var repository = GetRequiredService<IRepository<Appointment, Guid>>();
            var appointment = await repository.GetAsync(AppointmentsTestData.Appointment1Id);
            ObjectHelper.TrySetProperty(appointment, a => a.CreatorId, () => (Guid?)creator);
            await repository.UpdateAsync(appointment, autoSave: true);
        }
    });

    private Task SetPatientLoginAsync(string email) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
        {
            var login = (await GetRequiredService<IdentityUserManager>().FindByEmailAsync(email))!.Id;
            var appointment = await GetRequiredService<IRepository<Appointment, Guid>>().GetAsync(AppointmentsTestData.Appointment1Id);
            var patients = GetRequiredService<IRepository<Patient, Guid>>();
            var patient = await patients.GetAsync(appointment.PatientId);
            patient.IdentityUserId = login;
            await patients.UpdateAsync(patient, autoSave: true);
        }
    });

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

    private Task CreateNamedOfficeBUserAsync(string email, string name, string surname) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantBRef))
        {
            var user = new IdentityUser(Guid.NewGuid(), $"TEST-officeb-{Guid.NewGuid():N}", email, TenantsTestData.TenantBRef)
            {
                Name = name,
                Surname = surname,
            };
            (await GetRequiredService<IdentityUserManager>().CreateAsync(user)).Succeeded.ShouldBeTrue();
        }
    });

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
