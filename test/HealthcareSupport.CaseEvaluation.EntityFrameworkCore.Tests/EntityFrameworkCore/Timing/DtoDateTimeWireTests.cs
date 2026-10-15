using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments;
using Shouldly;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Timing;

/// <summary>
/// What every API DTO <c>DateTime</c> looks like when it reaches the browser -- the wire half of
/// <see cref="CalendarDateNormalizationTests"/>, which governs the database half.
///
/// <para>MEASURED 2026-10-15, through the options the HTTP API really uses
/// (<see cref="HttpApiJsonOptions"/>): of the DateTime properties on the Application.Contracts
/// types, exactly two reach the client without a zone. Every other one, calendar dates included,
/// is serialized as UTC, so a date of birth stored as 1985-09-03 leaves the server as
/// <c>1985-09-03T00:00:00Z</c>. Whether a screen then shows 1985-09-02 depends only on how that
/// screen parses it: the <c>calendarDate</c> pipe and <c>parseCalendarDate</c> strip the zone and
/// are right; <c>new Date()</c>, Angular's <c>date</c> pipe and the <c>pacificDate</c> pipe are
/// not.</para>
///
/// <para>This test RECORDS that state rather than changing it. Every DTO date is classified as an
/// instant (must keep its Z), an exempt calendar date (must not have one), or a calendar date
/// still going out as UTC (<see cref="CalendarDatesStillSentAsUtc"/>, the list a sweep works
/// down). It fails on a NEW DateTime nobody classified, on a stale entry, and on any field whose
/// wire form no longer matches its list -- so exempting a calendar date forces it to move lists,
/// and nothing slips back.</para>
///
/// <para>Not covered, deliberately: the <c>CreationTime</c> / <c>LastModificationTime</c> /
/// <c>DeletionTime</c> audit columns, instants by ABP convention and asserted generically below;
/// and <c>*Eto</c> event types, which travel on the event bus, not to a browser. The Case Tracker
/// feed is also outside this: its payload carries dates as pre-formatted strings.</para>
/// </summary>
public class DtoDateTimeWireTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private const string RootNamespace = "HealthcareSupport.CaseEvaluation.";

    private static readonly HashSet<string> AuditInstants = new(StringComparer.Ordinal)
    {
        "CreationTime",
        "LastModificationTime",
        "DeletionTime",
    };

    /// <summary>Calendar dates that already reach the client with no zone (#1368).</summary>
    private static readonly HashSet<string> ExemptCalendarDates = new(StringComparer.Ordinal)
    {
        "AppointmentChangeRequests.AppointmentChangeRequestDto.RequestedSlotDate",
        "AppointmentChangeRequests.AppointmentChangeRequestDto.CurrentRoundProposedDate",
    };

    /// <summary>
    /// Calendar dates that STILL reach the client as UTC midnight (measured 2026-10-15). Correct
    /// only where the screen parses them with <c>calendarDate</c> / <c>parseCalendarDate</c> or a
    /// string slice. Once one is exempted with <c>[DisableDateTimeNormalization]</c> on the DTO
    /// property, this test fails until it is moved to <see cref="ExemptCalendarDates"/>.
    /// </summary>
    private static readonly HashSet<string> CalendarDatesStillSentAsUtc = new(StringComparer.Ordinal)
    {
        "AppointmentChangeRequests.PartyConsentRequestDto.AppointmentDate",
        "AppointmentInfoRequests.InjuryDetailCorrectionDto.DateOfInjury",
        "AppointmentInfoRequests.InjuryDetailCorrectionDto.ToDateOfInjury",
        "AppointmentInjuryDetails.AppointmentInjuryDetailCreateDto.DateOfInjury",
        "AppointmentInjuryDetails.AppointmentInjuryDetailCreateDto.ToDateOfInjury",
        "AppointmentInjuryDetails.AppointmentInjuryDetailDto.DateOfInjury",
        "AppointmentInjuryDetails.AppointmentInjuryDetailDto.ToDateOfInjury",
        "AppointmentInjuryDetails.AppointmentInjuryDetailUpdateDto.DateOfInjury",
        "AppointmentInjuryDetails.AppointmentInjuryDetailUpdateDto.ToDateOfInjury",
        "Appointments.AppointmentCreateDto.AppointmentDate",
        "Appointments.AppointmentCreateDto.DueDate",
        "Appointments.AppointmentDto.AppointmentDate",
        "Appointments.AppointmentDto.DueDate",
        "Appointments.AppointmentSubmitDto.AppointmentDate",
        "Appointments.AppointmentSubmitDto.DueDate",
        "Appointments.AppointmentUpdateDto.AppointmentDate",
        "Appointments.AppointmentUpdateDto.DueDate",
        "Appointments.GetAppointmentsInput.AppointmentDateMin",
        "Appointments.GetAppointmentsInput.AppointmentDateMax",
        "Dashboards.DashboardDeadlineItemDto.DueDate",
        "Dashboards.DashboardScheduleItemDto.AppointmentDate",
        "Dashboards.DashboardTrendPointDto.WeekStart",
        "DoctorAvailabilities.DoctorAvailabilityCreateDto.AvailableDate",
        "DoctorAvailabilities.DoctorAvailabilityDeleteByDateInputDto.AvailableDate",
        "DoctorAvailabilities.DoctorAvailabilityDeleteBySlotInputDto.AvailableDate",
        "DoctorAvailabilities.DoctorAvailabilityDto.AvailableDate",
        "DoctorAvailabilities.DoctorAvailabilityGenerateInputDto.FromDate",
        "DoctorAvailabilities.DoctorAvailabilityGenerateInputDto.ToDate",
        "DoctorAvailabilities.DoctorAvailabilitySlotPreviewDto.AvailableDate",
        "DoctorAvailabilities.DoctorAvailabilityUpdateDto.AvailableDate",
        "DoctorAvailabilities.GetDoctorAvailabilitiesInput.AvailableDateMin",
        "DoctorAvailabilities.GetDoctorAvailabilitiesInput.AvailableDateMax",
        "DoctorAvailabilities.GetDoctorAvailabilityLookupInput.AvailableDateFrom",
        "DoctorAvailabilities.GetDoctorAvailabilityLookupInput.AvailableDateTo",
        "DoctorAvailabilities.GetScheduleInput.FromDate",
        "DoctorAvailabilities.GetScheduleInput.ToDate",
        "DoctorAvailabilities.ScheduleSlotDto.AvailableDate",
        "Patients.CreatePatientForAppointmentBookingInput.DateOfBirth",
        "Patients.GetPatientsInput.DateOfBirthMin",
        "Patients.GetPatientsInput.DateOfBirthMax",
        "Patients.PatientCreateDto.DateOfBirth",
        "Patients.PatientDto.DateOfBirth",
        "Patients.PatientUpdateDto.DateOfBirth",
        "Reports.AppointmentReportRowDto.AppointmentDate",
        "Reports.GetAppointmentReportInput.AppointmentDateMin",
        "Reports.GetAppointmentReportInput.AppointmentDateMax",
    };

    /// <summary>Moments that happened. Stored UTC; must keep the Z so the client converts them.</summary>
    private static readonly HashSet<string> Instants = new(StringComparer.Ordinal)
    {
        "AppointmentChangeLogs.AppointmentChangeLogDto.ChangeTime",
        "AppointmentChangeLogs.GetAppointmentChangeLogsInput.StartTime",
        "AppointmentChangeLogs.GetAppointmentChangeLogsInput.EndTime",
        "AppointmentChangeRequests.GetChangeRequestsInput.CreatedFromUtc",
        "AppointmentChangeRequests.GetChangeRequestsInput.CreatedToUtc",
        "AppointmentDocuments.AppointmentPacketDto.GeneratedAt",
        "AppointmentDocuments.AppointmentPacketDto.RegeneratedAt",
        "AppointmentDocuments.PatientPortalDocumentDto.CreatedAt",
        "AppointmentDrafts.AppointmentDraftDto.LastSavedTime",
        "AppointmentInfoRequests.AppointmentInfoRequestDto.ResolvedAt",
        "AppointmentInfoRequests.AppointmentInfoRequestRoundDto.RequestedAt",
        "AppointmentInfoRequests.AppointmentInfoRequestRoundDto.ResolvedAt",
        "Appointments.AppointmentDto.JointDeclarationOverdueAt",
        "Appointments.AppointmentDto.AppointmentApproveDate",
        "Appointments.RescheduleChainDto.SideAAgreedAt",
        "Appointments.RescheduleChainDto.SideBAgreedAt",
        "Appointments.RescheduleChainDto.DecidedAt",
        "Dashboards.DashboardActivityItemDto.When",
        "Dashboards.DashboardDeadlineItemDto.RequestedAt",
        "ExternalSignups.InvitationDto.ExpiresAt",
        "ExternalSignups.InvitationDto.AcceptedAt",
        "ExternalSignups.InvitationValidationDto.ExpiresAt",
        "ExternalSignups.InviteExternalUserResultDto.ExpiresAt",
        "Integration.CaseTracker.CaseTrackerDeadLetterDto.FailedAt",
        "Integration.CaseTracker.CaseTrackerDeadLetterDto.AlertedAt",
        "Integration.CaseTracker.CaseTrackerMissingIntakeItemDto.ApprovedAt",
        "Integration.CaseTracker.CaseTrackerMissingIntakeOfficeDto.FirstIntakeRowAt",
        "Integration.CaseTracker.CaseTrackerMissingIntakeReportDto.GeneratedAt",
        "Integration.CaseTracker.CaseTrackerOfficePushStateDto.FeedStartedAt",
        "Integration.CaseTracker.CaseTrackerOfficePushStateDto.LastRequestAt",
        "Integration.CaseTracker.CaseTrackerOfficePushStateDto.LastAdvancedAt",
        "Notifications.AppNotificationDto.ReadTime",
    };

    private static readonly DateTime Probe = new(2026, 10, 20, 0, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public void Every_dto_DateTime_is_classified_and_no_entry_is_stale()
    {
        var found = DateFields().Where(f => !AuditInstants.Contains(f.Property.Name))
            .Select(f => f.Key)
            .ToHashSet(StringComparer.Ordinal);
        var listed = Instants.Concat(ExemptCalendarDates).Concat(CalendarDatesStillSentAsUtc).ToList();

        listed.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key)
            .ShouldBeEmpty("A field is listed under two classifications.");

        found.Except(listed).OrderBy(k => k).ShouldBeEmpty(
            "These DTO DateTime properties are unclassified. Decide which kind of value each holds. A moment " +
            "that happened goes in Instants. A date somebody wrote down (a birth date, an injury date, an " +
            "appointment or slot date) gets [DisableDateTimeNormalization] on the DTO PROPERTY and goes in " +
            "ExemptCalendarDates -- the entity's attribute does not carry over to the DTO.");

        listed.Except(found).OrderBy(k => k).ShouldBeEmpty(
            "These entries name a DTO property that no longer exists; the property was renamed or removed.");
    }

    [Fact]
    public void Each_field_reaches_the_client_in_the_form_its_classification_says()
    {
        var options = HttpApiJsonOptions.Create(ServiceProvider);
        var wrong = new List<string>();

        foreach (var field in DateFields())
        {
            var wire = Serialize(field, options);
            var hasZone = wire.EndsWith('Z');

            if (ExemptCalendarDates.Contains(field.Key) && hasZone)
            {
                wrong.Add($"{field.Key} is listed as exempt but goes out as {wire}");
            }
            else if (CalendarDatesStillSentAsUtc.Contains(field.Key) && !hasZone)
            {
                wrong.Add($"{field.Key} now goes out as {wire}, with no zone: move it to ExemptCalendarDates");
            }
            else if ((Instants.Contains(field.Key) || AuditInstants.Contains(field.Property.Name)) && !hasZone)
            {
                wrong.Add($"{field.Key} is an instant but goes out as {wire}, so the client cannot convert it");
            }
        }

        wrong.ShouldBeEmpty(string.Join("; ", wrong));
    }

    [Fact]
    public void The_options_used_here_are_the_ones_that_add_the_zone()
    {
        // Control. If these options stopped running ABP's normalizing converter, every assertion
        // above would compare against the wrong serializer and could not fail for the right reason.
        var json = JsonSerializer.Serialize(
            new AppointmentChangeRequestDto { CreationTime = Probe, RequestedSlotDate = Probe },
            HttpApiJsonOptions.Create(ServiceProvider));

        json.ShouldContain("\"creationTime\":\"2026-10-20T00:00:00Z\"", Case.Sensitive);
        json.ShouldContain("\"requestedSlotDate\":\"2026-10-20T00:00:00\"", Case.Sensitive);
    }

    [Theory]
    [InlineData("2026-10-20T08:30:00Z")]
    [InlineData("2026-10-20T08:30:00")]
    public void A_calendar_value_sent_back_with_or_without_the_zone_keeps_its_wall_clock(string sent)
    {
        // Several screens echo a value they received (staff appointment edit, re-evaluation prefill,
        // unedited injury dates), so it comes back carrying the Z the server added. What matters is
        // that the request reads it back as the same date and time it went out as; the exempt column
        // then stores it unchanged.
        var input = JsonSerializer.Deserialize<AppointmentUpdateDto>(
            $"{{\"appointmentDate\":\"{sent}\"}}",
            HttpApiJsonOptions.Create(ServiceProvider))!;

        input.AppointmentDate.ToString("yyyy-MM-ddTHH:mm:ss").ShouldBe("2026-10-20T08:30:00");
    }

    private static string Serialize(DateField field, JsonSerializerOptions options)
    {
        // A type the test cannot build is judged by the attribute, which is exactly what ABP's
        // converter modifier reads; everything measured on 2026-10-15 could be built.
        if (field.Type.GetConstructor(Type.EmptyTypes) == null || field.Property.SetMethod is not { IsPublic: true })
        {
            var exempt = field.Property.IsDefined(typeof(DisableDateTimeNormalizationAttribute), true)
                || field.Type.IsDefined(typeof(DisableDateTimeNormalizationAttribute), true);
            return exempt ? "2026-10-20T00:00:00" : "2026-10-20T00:00:00Z";
        }

        var instance = Activator.CreateInstance(field.Type)!;
        field.Property.SetValue(instance, Probe);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(instance, field.Type, options));
        var name = options.PropertyNamingPolicy?.ConvertName(field.Property.Name) ?? field.Property.Name;
        return document.RootElement.GetProperty(name).GetString() ?? string.Empty;
    }

    private static IEnumerable<DateField> DateFields()
    {
        return typeof(AppointmentChangeRequestDto).Assembly.GetTypes()
            .Where(t => t.IsClass && t.IsPublic && !t.IsAbstract && !t.ContainsGenericParameters)
            .Where(t => !t.Name.EndsWith("Eto", StringComparison.Ordinal))
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTime?))
                .Select(p => new DateField(t, p)));
    }

    private sealed record DateField(Type Type, PropertyInfo Property)
    {
        public string Key => $"{Type.FullName![RootNamespace.Length..]}.{Property.Name}";
    }
}
