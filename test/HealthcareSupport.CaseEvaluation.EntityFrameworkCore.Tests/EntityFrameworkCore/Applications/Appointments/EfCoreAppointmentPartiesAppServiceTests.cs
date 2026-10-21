using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications.Delivery;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.ExternalSignups;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities.Events;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Pins <see cref="AppointmentPartiesAppService"/>, staff adding, changing and removing the
/// parties on an appointment after booking (2026-10-21).
///
/// <para><b>Why the end-to-end shape.</b> The defect this fixes was invisible in every narrower
/// test: a party added through "Edit details" got its master and link rows (both asserted
/// elsewhere) but never the appointment's party-email column, and that column is the ONLY thing
/// the read guard and the list consult for an attorney or claim examiner. So these facts go all
/// the way: staff add the party, the party REGISTERS through the real sign-up, and then the real
/// <see cref="AppointmentReadAccessGuard"/> and <see cref="AppointmentVisibilityService"/> decide.
/// Measured before the fix: the DA was refused on open and missing from the list (the probe that
/// found it).</para>
///
/// <para><b>What this does not prove.</b> The rig runs AddAlwaysAllowAuthorization(), so the
/// <c>Appointments.Edit</c> permission is not under test here; the real-authorization file
/// <c>AppointmentPartiesAuthorizationTests</c> covers it. The internal-caller check inside the
/// service is a body rule and IS under test (the external fact below).</para>
///
/// <para><b>Mail.</b> The dispatcher is a recorder for the whole class (the rig seeds no office
/// templates, so a real render would throw). The packet handlers, the recipient resolver and the
/// party columns they read are all real; what is recorded is exactly what they asked to send. The
/// once-per-address de-duplication sits behind the dispatcher, in the outbox, and has its own tests.</para>
///
/// <para>The rig's ambient principal holds "admin", an internal role, which is the staff caller.
/// Every fact builds its own appointment and patient with a fresh token; the collection shares
/// one database with no rollback.</para>
/// </summary>
public class EfCoreAppointmentPartiesAppServiceTests
    : CaseEvaluationApplicationTestBase<CaseEvaluationEntityFrameworkCoreTestModule>
{
    private const string Password = "Test1234!";

    private readonly IAppointmentPartiesAppService _parties;
    private readonly IExternalSignupAppService _signups;
    private readonly IdentityUserManager _users;
    private readonly AppointmentReadAccessGuard _guard;
    private readonly AppointmentVisibilityService _visibility;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IRepository<AppointmentDefenseAttorney, Guid> _defenseLinks;
    private readonly IRepository<AppointmentApplicantAttorney, Guid> _applicantLinks;
    private readonly IRepository<DefenseAttorney, Guid> _defenseMasters;
    private readonly IRepository<AppointmentClaimExaminer, Guid> _claimExaminers;
    private readonly IRepository<AppointmentPacket, Guid> _packets;
    private readonly IIntegrationOutboxRepository _outbox;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    private static readonly Guid BookerId = IdentityUsersTestData.ApplicantAttorney1UserId;

    private readonly NotificationRecorder _mail = new();

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<INotificationDispatcher>(_mail));
    }

    public EfCoreAppointmentPartiesAppServiceTests()
    {
        _parties = GetRequiredService<IAppointmentPartiesAppService>();
        _signups = GetRequiredService<IExternalSignupAppService>();
        _users = GetRequiredService<IdentityUserManager>();
        _guard = GetRequiredService<AppointmentReadAccessGuard>();
        _visibility = GetRequiredService<AppointmentVisibilityService>();
        _appointmentRepository = GetRequiredService<IAppointmentRepository>();
        _patientRepository = GetRequiredService<IRepository<Patient, Guid>>();
        _defenseLinks = GetRequiredService<IRepository<AppointmentDefenseAttorney, Guid>>();
        _applicantLinks = GetRequiredService<IRepository<AppointmentApplicantAttorney, Guid>>();
        _defenseMasters = GetRequiredService<IRepository<DefenseAttorney, Guid>>();
        _claimExaminers = GetRequiredService<IRepository<AppointmentClaimExaminer, Guid>>();
        _packets = GetRequiredService<IRepository<AppointmentPacket, Guid>>();
        _outbox = GetRequiredService<IIntegrationOutboxRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    // ----------------------------------------------------------------- add, then the party registers

    [Fact]
    public async Task Staff_add_an_unregistered_defense_attorney_who_then_registers_and_sees_the_case()
    {
        var token = NewToken();
        var email = $"da-{token}@example.test";
        var id = await CreateAppointmentAsync(token, AppointmentStatusType.Approved);

        await AsStaffAsync(() => _parties.UpdateAsync(id, new AppointmentPartiesUpdateDto
        {
            DefenseAttorney = Attorney(email, "TEST-Dana", "TEST-Defense"),
        }));

        await InOfficeAsync(async () =>
        {
            var appointment = await _appointmentRepository.GetAsync(id);
            appointment.DefenseAttorneyEmail.ShouldBe(email);
            appointment.DefenseAttorneyFirstName.ShouldBe("TEST-Dana", "the link manager snapshot ran");
            (await _defenseLinks.GetListAsync(l => l.AppointmentId == id)).ShouldHaveSingleItem()
                .IdentityUserId.ShouldBeNull("no account exists yet");
        });

        var accountId = await RegisterAsync(ExternalUserType.DefenseAttorney, email);

        await InOfficeAsync(async () =>
            (await _defenseLinks.GetListAsync(l => l.AppointmentId == id)).ShouldHaveSingleItem()
                .IdentityUserId.ShouldBe(accountId, "registration claims the link row by email"));
        (await AccessAsAsync(id, accountId, email, IdentityUsersTestData.DefenseAttorneyRoleName))
            .ShouldBe((true, true));
    }

    [Fact]
    public async Task Staff_add_an_unregistered_applicant_attorney_who_then_registers_and_sees_the_case()
    {
        var token = NewToken();
        var email = $"aa-{token}@example.test";
        var id = await CreateAppointmentAsync(token, AppointmentStatusType.Approved, bookerId: IdentityUsersTestData.ClaimExaminer1UserId);

        await AsStaffAsync(() => _parties.UpdateAsync(id, new AppointmentPartiesUpdateDto
        {
            ApplicantAttorney = Attorney(email, "TEST-Abe", "TEST-Applicant"),
        }));

        var accountId = await RegisterAsync(ExternalUserType.ApplicantAttorney, email);

        await InOfficeAsync(async () =>
        {
            (await _appointmentRepository.GetAsync(id)).ApplicantAttorneyEmail.ShouldBe(email);
            (await _applicantLinks.GetListAsync(l => l.AppointmentId == id)).ShouldHaveSingleItem()
                .IdentityUserId.ShouldBe(accountId);
        });
        (await AccessAsAsync(id, accountId, email, IdentityUsersTestData.ApplicantAttorneyRoleName))
            .ShouldBe((true, true));
    }

    [Fact]
    public async Task Staff_add_a_claim_examiner_after_approval_who_then_registers_and_sees_the_case()
    {
        var token = NewToken();
        var email = $"ce-{token}@example.test";
        var id = await CreateAppointmentAsync(token, AppointmentStatusType.Approved);

        await AsStaffAsync(() => _parties.UpdateAsync(id, new AppointmentPartiesUpdateDto
        {
            ClaimExaminer = new StaffClaimExaminerChangeDto { Name = "TEST-Cleo Examiner", Email = email, PhoneNumber = "8185550123" },
        }));

        await InOfficeAsync(async () =>
        {
            (await _appointmentRepository.GetAsync(id)).ClaimExaminerEmail.ShouldBe(email);
            var row = (await _claimExaminers.GetListAsync(x => x.AppointmentId == id)).ShouldHaveSingleItem();
            row.Email.ShouldBe(email);
            row.Name.ShouldBe("TEST-Cleo Examiner");
            row.IsActive.ShouldBeTrue("the Case Tracker payload reads only active rows");
        });

        var accountId = await RegisterAsync(ExternalUserType.ClaimExaminer, email);
        (await AccessAsAsync(id, accountId, email, IdentityUsersTestData.ClaimExaminerRoleName))
            .ShouldBe((true, true));
    }

    // ----------------------------------------------------------------- patient email correction

    [Fact]
    public async Task Correcting_the_patient_email_moves_access_from_the_old_address_to_the_new_one()
    {
        var token = NewToken();
        var oldEmail = $"pt-old-{token}@example.test";
        var newEmail = $"pt-new-{token}@example.test";
        var id = await CreateAppointmentAsync(token, AppointmentStatusType.Approved, patientEmail: oldEmail);
        var oldUser = Guid.NewGuid();
        var newUser = Guid.NewGuid();

        // Positive control: the OLD address opens it before the correction, so the refusal after
        // it is the correction, not a broken principal.
        (await AccessAsAsync(id, oldUser, oldEmail, IdentityUsersTestData.PatientRoleName)).ShouldBe((true, true));

        await AsStaffAsync(() => _parties.UpdateAsync(id, new AppointmentPartiesUpdateDto { PatientEmail = newEmail }));

        (await AccessAsAsync(id, newUser, newEmail, IdentityUsersTestData.PatientRoleName)).ShouldBe((true, true));
        (await AccessAsAsync(id, oldUser, oldEmail, IdentityUsersTestData.PatientRoleName)).ShouldBe((false, false));
    }

    // ----------------------------------------------------------------- remove and replace

    [Fact]
    public async Task Removing_the_defense_attorney_revokes_their_access_and_clears_the_snapshot()
    {
        var token = NewToken();
        var email = $"da-rm-{token}@example.test";
        var id = await CreateAppointmentAsync(token, AppointmentStatusType.Approved);
        await AsStaffAsync(() => _parties.UpdateAsync(id, new AppointmentPartiesUpdateDto
        {
            DefenseAttorney = Attorney(email, "TEST-Rex", "TEST-Removed"),
        }));
        var daUser = Guid.NewGuid();
        (await AccessAsAsync(id, daUser, email, IdentityUsersTestData.DefenseAttorneyRoleName)).ShouldBe((true, true));

        await AsStaffAsync(() => _parties.UpdateAsync(id, new AppointmentPartiesUpdateDto
        {
            DefenseAttorney = new StaffAttorneyChangeDto { Remove = true },
        }));

        await InOfficeAsync(async () =>
        {
            var appointment = await _appointmentRepository.GetAsync(id);
            appointment.DefenseAttorneyEmail.ShouldBeNull();
            appointment.DefenseAttorneyFirstName.ShouldBeNull();
            appointment.DefenseAttorneyFirmName.ShouldBeNull();
            (await _defenseLinks.GetListAsync(l => l.AppointmentId == id)).ShouldBeEmpty();
        });
        (await AccessAsAsync(id, daUser, email, IdentityUsersTestData.DefenseAttorneyRoleName)).ShouldBe((false, false));
    }

    [Fact]
    public async Task Replacing_an_attorney_never_rewrites_the_first_attorneys_record()
    {
        var token = NewToken();
        var first = $"da-a-{token}@example.test";
        var second = $"da-b-{token}@example.test";
        var id = await CreateAppointmentAsync(token, AppointmentStatusType.Approved);
        await AsStaffAsync(() => _parties.UpdateAsync(id, new AppointmentPartiesUpdateDto
        {
            DefenseAttorney = Attorney(first, "TEST-First", "TEST-Attorney"),
        }));
        var firstMasterId = await InOfficeAsync(async () =>
            (await _defenseLinks.GetListAsync(l => l.AppointmentId == id)).Single().DefenseAttorneyId);

        // The page still holds the first attorney's record id when staff type the second one in.
        var replacement = Attorney(second, "TEST-Second", "TEST-Attorney");
        replacement.AttorneyId = firstMasterId;
        await AsStaffAsync(() => _parties.UpdateAsync(id, new AppointmentPartiesUpdateDto { DefenseAttorney = replacement }));

        await InOfficeAsync(async () =>
        {
            var firstMaster = await _defenseMasters.GetAsync(firstMasterId);
            firstMaster.Email.ShouldBe(first, "the first attorney's record must keep its own email");
            firstMaster.FirstName.ShouldBe("TEST-First");
            var link = (await _defenseLinks.GetListAsync(l => l.AppointmentId == id)).ShouldHaveSingleItem();
            link.DefenseAttorneyId.ShouldNotBe(firstMasterId);
            (await _defenseMasters.GetAsync(link.DefenseAttorneyId)).Email.ShouldBe(second);
            (await _appointmentRepository.GetAsync(id)).DefenseAttorneyEmail.ShouldBe(second);
        });
    }

    // ----------------------------------------------------------------- refusals

    [Fact]
    public async Task An_external_caller_is_refused_and_no_column_is_written()
    {
        var token = NewToken();
        var id = await CreateAppointmentAsync(token, AppointmentStatusType.Approved);

        // The booker is an external party ON the case, so the read guard alone would admit them.
        // Only the staff-only check stands between them and the access-key column.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, BookerId,
                   IdentityUsersTestData.ApplicantAttorney1Email, IdentityUsersTestData.ApplicantAttorneyRoleName))
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _parties.UpdateAsync(id, new AppointmentPartiesUpdateDto
            {
                DefenseAttorney = Attorney($"planted-{token}@example.test", "TEST-Planted", "TEST-Self"),
                PatientEmail = $"planted-pt-{token}@example.test",
            }));
        }

        await InOfficeAsync(async () =>
        {
            var appointment = await _appointmentRepository.GetAsync(id);
            appointment.DefenseAttorneyEmail.ShouldBeNull();
            appointment.PatientEmail.ShouldBe($"pt-{token}@example.test");
            (await _defenseLinks.GetListAsync(l => l.AppointmentId == id)).ShouldBeEmpty();
        });
    }

    [Fact]
    public async Task An_attorney_without_an_email_is_refused_before_anything_is_written()
    {
        var token = NewToken();
        var id = await CreateAppointmentAsync(token, AppointmentStatusType.Approved);
        var noEmail = Attorney("unused@example.test", "TEST-No", "TEST-Email");
        noEmail.Email = null;

        await Should.ThrowAsync<UserFriendlyException>(() => AsStaffAsync(() =>
            _parties.UpdateAsync(id, new AppointmentPartiesUpdateDto { DefenseAttorney = noEmail })));

        await InOfficeAsync(async () =>
            (await _defenseLinks.GetListAsync(l => l.AppointmentId == id)).ShouldBeEmpty());
    }

    // ----------------------------------------------------------------- Case Tracker

    [Fact]
    public async Task A_party_change_on_an_approved_appointment_queues_the_updated_intake_for_the_case_tracker()
    {
        var token = NewToken();
        var email = $"da-ct-{token}@example.test";
        var id = await CreateAppointmentAsync(token, AppointmentStatusType.Approved);
        await SeedSettledPacketsAsync(id);

        await AsStaffAsync(() => _parties.UpdateAsync(id, new AppointmentPartiesUpdateDto
        {
            DefenseAttorney = Attorney(email, "TEST-Ct", "TEST-Defense"),
        }));

        var intakes = await IntakeRowsAsync(id);
        intakes.ShouldNotBeEmpty("the change must reach the Case Tracker");
        intakes.ShouldContain(r => r.Payload.Contains(email, StringComparison.OrdinalIgnoreCase),
            "the queued intake carries the new defense attorney's email");
    }

    [Fact]
    public Task A_claim_examiner_only_change_still_queues_an_intake() =>
        ClaimExaminerOnlyChangeQueuesAnIntakeAsync();

    [Fact]
    public async Task A_claim_examiner_only_change_is_queued_without_the_frameworks_navigation_event()
    {
        // The CE edit changes no column on the appointment row (the email is unchanged). ABP still
        // publishes the appointment's update event, because the examiner row's foreign key points at
        // it (PublishEntityUpdatedEventWhenNavigationChanges, on by default), and that alone carries
        // the test above. Measured 2026-10-21: with the option off and the service's explicit re-push
        // removed, nothing is queued. This test turns the option off, so the explicit re-push is what
        // it proves.
        var options = GetRequiredService<IOptions<AbpEntityChangeOptions>>().Value;
        options.PublishEntityUpdatedEventWhenNavigationChanges = false;
        try
        {
            await ClaimExaminerOnlyChangeQueuesAnIntakeAsync();
        }
        finally
        {
            options.PublishEntityUpdatedEventWhenNavigationChanges = true;
        }
    }

    private async Task ClaimExaminerOnlyChangeQueuesAnIntakeAsync()
    {
        var token = NewToken();
        var email = $"ce-ct-{token}@example.test";
        var id = await CreateAppointmentAsync(token, AppointmentStatusType.Approved);
        await SeedSettledPacketsAsync(id);
        await AsStaffAsync(() => _parties.UpdateAsync(id, new AppointmentPartiesUpdateDto
        {
            ClaimExaminer = new StaffClaimExaminerChangeDto { Name = "TEST-Before", Email = email },
        }));
        var before = (await IntakeRowsAsync(id)).Count;

        await AsStaffAsync(() => _parties.UpdateAsync(id, new AppointmentPartiesUpdateDto
        {
            ClaimExaminer = new StaffClaimExaminerChangeDto { Name = "TEST-After", Email = email },
        }));

        var after = await IntakeRowsAsync(id);
        after.Count.ShouldBe(before + 1);
        after.ShouldContain(r => r.Payload.Contains("TEST-After", StringComparison.Ordinal));
    }

    // ----------------------------------------------------------------- the new party's packet email

    [Fact]
    public async Task A_new_defense_attorney_on_an_approved_appointment_is_sent_the_attorney_packet()
    {
        var token = NewToken();
        var email = $"da-mail-{token}@example.test";
        var id = await CreateAppointmentAsync(token, AppointmentStatusType.Approved);
        var packets = await SeedSettledPacketsAsync(id);
        _mail.Clear();

        await AsStaffAsync(() => _parties.UpdateAsync(id, new AppointmentPartiesUpdateDto
        {
            DefenseAttorney = Attorney(email, "TEST-Mail", "TEST-Defense"),
        }));

        var sent = _mail.Sent.Where(m => m.Recipients.Contains(email)).ToList();
        sent.ShouldHaveSingleItem("exactly one packet email to the new attorney");
        sent[0].TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.AppointmentDocumentAddWithAttachment);
        sent[0].Packet.ShouldNotBeNull();
        sent[0].Packet!.Kind.ShouldBe(PacketKind.AttorneyClaimExaminer);
        sent[0].Packet!.PacketId.ShouldBe(packets[PacketKind.AttorneyClaimExaminer]);
        _mail.Sent.ShouldNotContain(m => m.Packet != null && m.Packet.Kind == PacketKind.Patient,
            "an attorney change does not re-send the patient's packet");
    }

    [Fact]
    public async Task A_corrected_patient_email_is_sent_the_patient_packet()
    {
        var token = NewToken();
        var newEmail = $"pt-mail-{token}@example.test";
        var id = await CreateAppointmentAsync(token, AppointmentStatusType.Approved);
        var packets = await SeedSettledPacketsAsync(id);
        // The staff save corrects the Patient record first (the existing patient update), then the
        // appointment's own column through the parties call. Mirror that order.
        await InOfficeAsync(async () =>
        {
            var appointment = await _appointmentRepository.GetAsync(id);
            var patient = await _patientRepository.GetAsync(appointment.PatientId);
            patient.Email = newEmail;
            await _patientRepository.UpdateAsync(patient, autoSave: true);
        });
        _mail.Clear();

        await AsStaffAsync(() => _parties.UpdateAsync(id, new AppointmentPartiesUpdateDto { PatientEmail = newEmail }));

        var sent = _mail.Sent.ShouldHaveSingleItem();
        sent.Recipients.ShouldBe(new[] { newEmail });
        sent.Packet!.Kind.ShouldBe(PacketKind.Patient);
        sent.Packet.PacketId.ShouldBe(packets[PacketKind.Patient]);
    }

    [Fact]
    public async Task Before_approval_nothing_is_sent_because_approval_mails_everyone()
    {
        var token = NewToken();
        var id = await CreateAppointmentAsync(token, AppointmentStatusType.Pending);
        await SeedSettledPacketsAsync(id);
        _mail.Clear();

        await AsStaffAsync(() => _parties.UpdateAsync(id, new AppointmentPartiesUpdateDto
        {
            DefenseAttorney = Attorney($"da-pend-{token}@example.test", "TEST-Pend", "TEST-Defense"),
        }));

        _mail.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_packet_still_rendering_is_not_sent_now_because_its_own_completion_mails_everyone()
    {
        var token = NewToken();
        var id = await CreateAppointmentAsync(token, AppointmentStatusType.Approved);
        await SeedSettledPacketsAsync(id, PacketGenerationStatus.Generating);
        _mail.Clear();

        await AsStaffAsync(() => _parties.UpdateAsync(id, new AppointmentPartiesUpdateDto
        {
            DefenseAttorney = Attorney($"da-gen-{token}@example.test", "TEST-Gen", "TEST-Defense"),
        }));

        _mail.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Changing_an_existing_partys_details_but_not_their_email_sends_nothing()
    {
        var token = NewToken();
        var email = $"da-same-{token}@example.test";
        var id = await CreateAppointmentAsync(token, AppointmentStatusType.Approved);
        await SeedSettledPacketsAsync(id);
        await AsStaffAsync(() => _parties.UpdateAsync(id, new AppointmentPartiesUpdateDto
        {
            DefenseAttorney = Attorney(email, "TEST-Same", "TEST-Defense"),
        }));
        // Positive control: the first save above DID mail this address.
        _mail.Sent.ShouldContain(m => m.Recipients.Contains(email));
        _mail.Clear();

        var newPhone = Attorney(email.ToUpperInvariant(), "TEST-Same", "TEST-Defense");
        newPhone.PhoneNumber = "8185550199";
        await AsStaffAsync(() => _parties.UpdateAsync(id, new AppointmentPartiesUpdateDto { DefenseAttorney = newPhone }));

        _mail.Sent.ShouldBeEmpty("the same address (any capitalisation) is not a new party");
    }

    // ----------------------------------------------------------------- helpers

    private static string NewToken() => Guid.NewGuid().ToString("N")[..8];

    private static StaffAttorneyChangeDto Attorney(string email, string first, string last) => new()
    {
        Email = email,
        FirstName = first,
        LastName = last,
        FirmName = "TEST-Firm",
        PhoneNumber = "8185550100",
    };

    private async Task AsStaffAsync(Func<Task> call)
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await call();
        }
    }

    private async Task InOfficeAsync(Func<Task> action)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await action();
            }
        });
    }

    private async Task<T> InOfficeAsync<T>(Func<Task<T>> action)
    {
        return await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                return await action();
            }
        });
    }

    private Task<Guid> RegisterAsync(ExternalUserType type, string email) =>
        InOfficeAsync(async () =>
        {
            await _signups.RegisterAsync(new ExternalUserSignUpDto
            {
                UserType = type,
                Email = email,
                Password = Password,
                ConfirmPassword = Password,
                FirstName = "Synthetic",
                LastName = "Party",
                FirmName = "Synthetic Firm",
                TenantId = TenantsTestData.TenantARef,
            });
            return (await _users.FindByEmailAsync(email))!.Id;
        });

    /// <summary>(can open it, sees it in the list) for a caller with this verified email and role.</summary>
    private Task<(bool canRead, bool listed)> AccessAsAsync(Guid id, Guid userId, string email, string role) =>
        InOfficeAsync(async () =>
        {
            using (WithCurrentUser.RunWithEmail(_principalAccessor, userId, email, role))
            {
                var appointment = await _appointmentRepository.GetAsync(id);
                var canRead = await _guard.CanReadAsync(appointment);
                var visible = await _visibility.GetVisibleAppointmentIdsAsync();
                return (canRead, visible != null && visible.Contains(id));
            }
        });

    private Task<System.Collections.Generic.List<IntegrationOutboxItem>> IntakeRowsAsync(Guid id) =>
        InOfficeAsync(async () =>
            (await _outbox.GetListAsync()).Where(r => r.AppointmentId == id && r.MessageType == IntegrationMessageType.Intake).ToList());

    private Task<System.Collections.Generic.Dictionary<PacketKind, Guid>> SeedSettledPacketsAsync(
        Guid id, PacketGenerationStatus status = PacketGenerationStatus.Generated) =>
        InOfficeAsync(async () =>
        {
            var ids = new System.Collections.Generic.Dictionary<PacketKind, Guid>();
            foreach (var kind in new[] { PacketKind.Patient, PacketKind.Doctor, PacketKind.AttorneyClaimExaminer })
            {
                var packet = await _packets.InsertAsync(new AppointmentPacket(
                    Guid.NewGuid(), TenantsTestData.TenantARef, id, kind, $"TEST-{id:N}-{kind}.pdf", status), autoSave: true);
                ids[kind] = packet.Id;
            }
            return ids;
        });

    private async Task<Guid> CreateAppointmentAsync(
        string token, AppointmentStatusType status, string? patientEmail = null, Guid? bookerId = null)
    {
        var appointmentId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var booker = bookerId ?? BookerId;
        var email = patientEmail ?? $"pt-{token}@example.test";
        await InOfficeAsync(async () =>
        {
            await _patientRepository.InsertAsync(new Patient(
                id: patientId,
                stateId: null,
                appointmentLanguageId: null,
                identityUserId: null,
                tenantId: TenantsTestData.TenantARef,
                firstName: $"TEST-{token}",
                lastName: $"TEST-{token}",
                email: email,
                genderId: (Gender)PatientsTestData.PatientGenderIdValue,
                dateOfBirth: PatientsTestData.FixedDateOfBirth,
                phoneNumberTypeId: (PhoneNumberType)PatientsTestData.PatientPhoneNumberTypeIdValue), autoSave: true);

            var appointment = new Appointment(
                id: appointmentId,
                patientId: patientId,
                identityUserId: booker,
                appointmentTypeId: LocationsTestData.AppointmentType1Id,
                locationId: LocationsTestData.Location1Id,
                doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot1Id,
                appointmentDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                requestConfirmationNumber: $"A9-PT-{token}",
                appointmentStatus: status)
            {
                TenantId = TenantsTestData.TenantARef,
                PatientEmail = email,
            };
            appointment.RecordBookedBy(booker);
            await _appointmentRepository.InsertAsync(appointment, autoSave: true);
        });
        return appointmentId;
    }
}
