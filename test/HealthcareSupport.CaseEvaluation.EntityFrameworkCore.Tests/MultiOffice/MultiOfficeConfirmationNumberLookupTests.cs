using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Outbox;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.PhiAccess;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.Settings;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.SettingManagement;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// R3 (2026-11-06) -- "I don't know the confirmation number". A booker gives the patient's name, date of
/// birth and a claim number; one matching patient's case gets its number emailed to the booker's own
/// verified login, anything else sends nothing, and the answer is the same either way.
///
/// <para>A partial of <see cref="MultiOfficeAtomicBookingSubmitTests"/> so it reuses that class's
/// seeding (the shared MultiOffice database, the slot and booking helpers) rather than copying it.
/// These tests book at 20:00-21:00, an hour no other MultiOffice test uses, each on its own day.</para>
/// </summary>
public partial class MultiOfficeAtomicBookingSubmitTests
{
    private IConfirmationNumberLookupsAppService Lookups => GetRequiredService<IConfirmationNumberLookupsAppService>();
    private IRepository<NotificationOutboxItem, Guid> Outbox => GetRequiredService<IRepository<NotificationOutboxItem, Guid>>();
    private IRepository<AppointmentInjuryDetail, Guid> Injuries => GetRequiredService<IRepository<AppointmentInjuryDetail, Guid>>();

    private const string LookupContext = "ConfirmationNumberLookup/";

    /// <summary>What a booker would type for a case booked through <c>CreateReEvaluationSourceAsync</c>.</summary>
    private async Task<ConfirmationNumberLookupDto> DetailsOfAsync(SeededOffice office, Guid appointmentId, Guid patientId)
    {
        ConfirmationNumberLookupDto? details = null;
        await InOfficeAsync(office, async () =>
        {
            var patient = await _patientRepository.GetAsync(patientId);
            var claim = (await Injuries.GetListAsync(i => i.AppointmentId == appointmentId)).First().ClaimNumber;
            details = new ConfirmationNumberLookupDto
            {
                PatientFirstName = patient.FirstName,
                PatientLastName = patient.LastName,
                PatientDateOfBirth = patient.DateOfBirth.Date,
                ClaimNumber = claim,
            };
        });
        return details!;
    }

    /// <summary>An approved case (or <paramref name="status"/>) for an existing patient, with one injury row carrying <paramref name="claim"/>.</summary>
    private async Task<Guid> SeedApprovedCaseAsync(
        SeededOffice office, Guid patientId, int day, string claim,
        AppointmentStatusType status = AppointmentStatusType.Approved)
    {
        var appointmentId = Guid.NewGuid();
        await InOfficeAsync(office, async () =>
        {
            var date = TestToday.AddDays(day);
            var slotId = await InsertSlotAsync(office, date, new TimeOnly(20, 0), new TimeOnly(21, 0));
            await _appointmentRepository.InsertAsync(new Appointment(
                id: appointmentId,
                patientId: patientId,
                identityUserId: null,
                appointmentTypeId: office.AppointmentTypeId,
                locationId: office.LocationId,
                doctorAvailabilityId: slotId,
                appointmentDate: date.AddHours(20),
                requestConfirmationNumber: "R3-" + Guid.NewGuid().ToString("N")[..8],
                appointmentStatus: status), autoSave: true);
            await Injuries.InsertAsync(new AppointmentInjuryDetail(
                Guid.NewGuid(), appointmentId, new DateTime(2025, 2, 3), claim, false, "Lower back",
                wcabAdj: "ADJ0000001"), autoSave: true);
        });
        return appointmentId;
    }

    private async Task<List<NotificationOutboxItem>> LookupMailAsync(SeededOffice office, Guid appointmentId)
    {
        var rows = new List<NotificationOutboxItem>();
        await InOfficeAsync(office, async () =>
            rows.AddRange(await Outbox.GetListAsync(
                x => x.Context.Contains(LookupContext) && x.Context.Contains(appointmentId.ToString()))));
        return rows;
    }

    private Task AsUnverifiedAsync(SeededOffice office, Guid userId, string login, Func<Task> body) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(office.OfficeId))
            using (WithCurrentUser.RunWithEmail(_principalAccessor, userId, login, emailVerified: false, "Applicant Attorney"))
            {
                await body();
            }
        }, requiresNew: true);

    [Fact]
    public async Task NumberLookup_OneMatch_EmailsTheNumberToTheAskersOwnLogin_AndLogsIt()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var source = await CreateReEvaluationSourceAsync(office, day: 71, dayOffset: 1000);
        var details = await DetailsOfAsync(office, source.Id, source.PatientId);
        var asker = Guid.NewGuid();
        const string login = "r3-asker-1@example.test";

        await InOfficeAsWithLoginAsync(office, asker, login, "Applicant Paralegal", () => Lookups.RequestAsync(details));

        var mail = await LookupMailAsync(office, source.Id);
        mail.ShouldHaveSingleItem("the number goes to the asker only; this office has no inbox set");
        mail[0].To.ShouldBe(login);
        mail[0].Subject.ShouldContain(NotificationTemplateConsts.Codes.ConfirmationNumberLookup);

        var row = (await PhiRowsForAsync(office, asker)).ShouldHaveSingleItem();
        row.Action.ShouldBe(PhiAccessAction.ConfirmationNumberLookupMatched);
        row.AppointmentId.ShouldBe(source.Id);
    }

    [Fact]
    public async Task NumberLookup_IgnoresCaseSpacesHyphensAndClaimDashes()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var suffix = Guid.NewGuid().ToString("N")[..6];
        Patient? patient = null;
        await InOfficeAsync(office, async () => patient = await InsertPatientAsync(office, null, suffix));
        // InsertPatientAsync names the patient "Original" / "Holder<suffix>"; the claim is typed differently.
        var appointmentId = await SeedApprovedCaseAsync(office, patient!.Id, day: 72, claim: $"CLM-{suffix}-77");

        var typed = new ConfirmationNumberLookupDto
        {
            PatientFirstName = "  ORIGINAL ",
            PatientLastName = $"holder{suffix}".ToUpperInvariant(),
            PatientDateOfBirth = patient.DateOfBirth.Date,
            ClaimNumber = $"clm {suffix} 77",
        };
        await InOfficeAsWithLoginAsync(office, Guid.NewGuid(), "r3-asker-2@example.test", "Applicant Attorney",
            () => Lookups.RequestAsync(typed));

        (await LookupMailAsync(office, appointmentId)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task NumberLookup_AWrongDetailSendsNothing_AndLogsANamelessRefusal()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var source = await CreateReEvaluationSourceAsync(office, day: 73, dayOffset: 1010);
        var details = await DetailsOfAsync(office, source.Id, source.PatientId);
        details.ClaimNumber = "NOT-THE-CLAIM";
        var asker = Guid.NewGuid();

        await InOfficeAsWithLoginAsync(office, asker, "r3-asker-3@example.test", "Applicant Attorney",
            () => Lookups.RequestAsync(details));

        (await LookupMailAsync(office, source.Id)).ShouldBeEmpty();
        var row = (await PhiRowsForAsync(office, asker)).ShouldHaveSingleItem();
        row.Action.ShouldBe(PhiAccessAction.ConfirmationNumberLookupNotMatched);
        row.AppointmentId.ShouldBeNull();
        row.PatientId.ShouldBeNull();
    }

    [Fact]
    public async Task NumberLookup_AnUnverifiedLoginGetsNothing()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var source = await CreateReEvaluationSourceAsync(office, day: 74, dayOffset: 1020);
        var details = await DetailsOfAsync(office, source.Id, source.PatientId);

        await AsUnverifiedAsync(office, Guid.NewGuid(), "r3-unverified@example.test", () => Lookups.RequestAsync(details));

        (await LookupMailAsync(office, source.Id)).ShouldBeEmpty("an unconfirmed address proves nothing about who reads it");
    }

    [Fact]
    public async Task NumberLookup_TwoPatientsWithTheSameDetails_IsAmbiguous_AndSendsNothing()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var suffix = Guid.NewGuid().ToString("N")[..6];
        Patient? first = null;
        Patient? second = null;
        await InOfficeAsync(office, async () =>
        {
            first = await InsertPatientAsync(office, null, suffix);
            second = await InsertPatientAsync(office, null, suffix + "b");
            second.LastName = first.LastName;
            await _patientRepository.UpdateAsync(second, autoSave: true);
        });
        var claim = $"CLM-{suffix}-AMB";
        var a = await SeedApprovedCaseAsync(office, first!.Id, day: 75, claim);
        var b = await SeedApprovedCaseAsync(office, second!.Id, day: 76, claim);

        await InOfficeAsWithLoginAsync(office, Guid.NewGuid(), "r3-asker-4@example.test", "Applicant Attorney",
            () => Lookups.RequestAsync(new ConfirmationNumberLookupDto
            {
                PatientFirstName = first.FirstName,
                PatientLastName = first.LastName,
                PatientDateOfBirth = first.DateOfBirth.Date,
                ClaimNumber = claim,
            }));

        (await LookupMailAsync(office, a)).ShouldBeEmpty();
        (await LookupMailAsync(office, b)).ShouldBeEmpty();
    }

    [Fact]
    public async Task NumberLookup_OnePatientWithTwoCasesUnderTheClaim_SendsTheLatest()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var suffix = Guid.NewGuid().ToString("N")[..6];
        Patient? patient = null;
        await InOfficeAsync(office, async () => patient = await InsertPatientAsync(office, null, suffix));
        var claim = $"CLM-{suffix}-TWO";
        var earlier = await SeedApprovedCaseAsync(office, patient!.Id, day: 77, claim);
        var later = await SeedApprovedCaseAsync(office, patient.Id, day: 78, claim);

        await InOfficeAsWithLoginAsync(office, Guid.NewGuid(), "r3-asker-5@example.test", "Applicant Attorney",
            () => Lookups.RequestAsync(new ConfirmationNumberLookupDto
            {
                PatientFirstName = patient.FirstName,
                PatientLastName = patient.LastName,
                PatientDateOfBirth = patient.DateOfBirth.Date,
                ClaimNumber = claim,
            }));

        (await LookupMailAsync(office, later)).ShouldHaveSingleItem("the latest exam is the one to follow on from");
        (await LookupMailAsync(office, earlier)).ShouldBeEmpty();
    }

    [Fact]
    public async Task NumberLookup_SkipsACaseThatCannotBeReEvaluated_EvenWhenItIsTheLatest()
    {
        // A FIRST evaluation that no-showed cannot be re-evaluated (the form would refuse it), so its number
        // is never sent, even though it is the newest case under the claim; the approved one is.
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var suffix = Guid.NewGuid().ToString("N")[..6];
        Patient? patient = null;
        await InOfficeAsync(office, async () => patient = await InsertPatientAsync(office, null, suffix));
        var claim = $"CLM-{suffix}-NS";
        var approved = await SeedApprovedCaseAsync(office, patient!.Id, day: 80, claim);
        var noShow = await SeedApprovedCaseAsync(office, patient.Id, day: 81, claim, AppointmentStatusType.NoShow);

        await InOfficeAsWithLoginAsync(office, Guid.NewGuid(), "r3-asker-6@example.test", "Applicant Attorney",
            () => Lookups.RequestAsync(new ConfirmationNumberLookupDto
            {
                PatientFirstName = patient.FirstName,
                PatientLastName = patient.LastName,
                PatientDateOfBirth = patient.DateOfBirth.Date,
                ClaimNumber = claim,
            }));

        (await LookupMailAsync(office, approved)).ShouldHaveSingleItem();
        (await LookupMailAsync(office, noShow)).ShouldBeEmpty("a first evaluation that no-showed cannot be re-evaluated");
    }

    [Fact]
    public async Task NumberLookup_TellsTheOfficeInboxWhoReceivedTheNumber()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var source = await CreateReEvaluationSourceAsync(office, day: 79, dayOffset: 1030);
        var details = await DetailsOfAsync(office, source.Id, source.PatientId);
        const string inbox = "r3-office-inbox@example.test";

        // The MultiOffice database is shared and never rolls back, and this collection runs one test at a
        // time, so the inbox is set for this test only and removed again in finally.
        await SetOfficeInboxAsync(office, inbox);
        try
        {
            await InOfficeAsWithLoginAsync(office, Guid.NewGuid(), "r3-asker-6@example.test", "Applicant Attorney",
                () => Lookups.RequestAsync(details));
        }
        finally
        {
            await SetOfficeInboxAsync(office, null);
        }

        var mail = await LookupMailAsync(office, source.Id);
        mail.Count.ShouldBe(2);
        mail.Single(m => m.To == inbox).Subject
            .ShouldContain(NotificationTemplateConsts.Codes.ConfirmationNumberLookupOffice);
    }

    [Fact]
    public async Task NumberLookup_IsRefusedAfterTheHourlyLimitForOneAccount()
    {
        var (office, _) = await GetSeededOfficesAsync();
        var asker = Guid.NewGuid();
        var guess = new ConfirmationNumberLookupDto
        {
            PatientFirstName = "Nobody",
            PatientLastName = "Known",
            PatientDateOfBirth = new DateTime(1980, 1, 1),
            ClaimNumber = "NONE",
        };

        for (var attempt = 0; attempt < LookupThrottleBudget.ConfirmationNumber.MaxPerUserPerHour; attempt++)
        {
            await InOfficeAsWithLoginAsync(office, asker, "r3-asker-7@example.test", "Applicant Attorney",
                () => Lookups.RequestAsync(guess));
        }

        var refused = await Should.ThrowAsync<BusinessException>(() =>
            InOfficeAsWithLoginAsync(office, asker, "r3-asker-7@example.test", "Applicant Attorney",
                () => Lookups.RequestAsync(guess)));
        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ReEvaluationLookupThrottled);
    }

    private Task SetOfficeInboxAsync(SeededOffice office, string? inbox) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(office.OfficeId))
            {
                await GetRequiredService<ISettingManager>().SetForCurrentTenantAsync(
                    CaseEvaluationSettings.NotificationsPolicy.OfficeEmail, inbox);
            }
        }, requiresNew: true);
}
