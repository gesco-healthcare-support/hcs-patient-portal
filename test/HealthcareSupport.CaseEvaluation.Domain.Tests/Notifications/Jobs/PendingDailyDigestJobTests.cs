using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Patients;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Jobs;

/// <summary>
/// Unit coverage for <see cref="PendingDailyDigestJob"/>, the 09:00 daily email to the office's
/// intake inbox listing every appointment request still waiting for a decision.
///
/// <para><b>WHAT IS PINNED.</b> Only PENDING requests are listed; each row carries the patient's
/// name (or "(unnamed patient)" when the patient row is gone); rows are in appointment-date order;
/// and an office with nothing pending publishes NO digest rather than an empty one.</para>
///
/// <para><b>The result asserted is the published <see cref="PendingDailyDigestEto"/></b> -- the job's
/// only output. No database: both repositories return in-memory lists, which the job's synchronous
/// LINQ reads directly. The tenant runner invokes the per-office delegate once for a fixed office.</para>
///
/// <para>Synthetic data only (HIPAA).</para>
/// </summary>
public class PendingDailyDigestJobTests
{
    private static readonly Guid OfficeId = new("33333333-3333-3333-3333-333333333333");

    private sealed class Harness
    {
        public List<Appointment> Appointments { get; } = new();
        public List<Patient> Patients { get; } = new();
        public ILocalEventBus Bus { get; } = Substitute.For<ILocalEventBus>();
        public PendingDailyDigestJob Job { get; }

        public Harness()
        {
            var appointmentRepository = Substitute.For<IRepository<Appointment, Guid>>();
            appointmentRepository.GetQueryableAsync().Returns(_ => Appointments.AsQueryable());
            var patientRepository = Substitute.For<IRepository<Patient, Guid>>();
            patientRepository.GetQueryableAsync().Returns(_ => Patients.AsQueryable());
            var tenantRunner = Substitute.For<ITenantWorkRunner>();
            tenantRunner.ForEachOfficeAsync(Arg.Any<Func<Guid, Task>>())
                .Returns(ci => ci.Arg<Func<Guid, Task>>()(OfficeId));

            Job = new PendingDailyDigestJob(
                appointmentRepository,
                patientRepository,
                tenantRunner,
                Bus,
                NullLogger<PendingDailyDigestJob>.Instance);
        }

        /// <summary>Every digest the job published, read back from the substitute bus.</summary>
        public List<PendingDailyDigestEto> Digests() =>
            Bus.ReceivedCalls().SelectMany(c => c.GetArguments()).OfType<PendingDailyDigestEto>().ToList();
    }

    private static Patient NewPatient(string firstName, string lastName) => new(
        id: Guid.NewGuid(),
        stateId: null,
        appointmentLanguageId: null,
        identityUserId: null,
        tenantId: OfficeId,
        firstName: firstName,
        lastName: lastName,
        email: "TEST-patient@test.local",
        genderId: Gender.Unspecified,
        dateOfBirth: new DateTime(1980, 1, 1),
        phoneNumberTypeId: PhoneNumberType.Home);

    private static Appointment NewAppointment(
        Guid patientId, AppointmentStatusType status, DateTime appointmentDate, string confirmation, DateTime? dueDate = null) =>
        new(
            id: Guid.NewGuid(),
            patientId: patientId,
            identityUserId: null,
            appointmentTypeId: Guid.NewGuid(),
            locationId: Guid.NewGuid(),
            doctorAvailabilityId: Guid.NewGuid(),
            appointmentDate: appointmentDate,
            requestConfirmationNumber: confirmation,
            appointmentStatus: status,
            dueDate: dueDate);

    /// <summary>
    /// <b>POSITIVE CONTROL.</b> One pending request produces exactly one digest, for this office,
    /// naming the patient. The "publishes nothing" Fact below depends on this one reaching the bus.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_OnePendingRequest_PublishesADigestNamingThePatient_PositiveControl()
    {
        var h = new Harness();
        var patient = NewPatient("TEST-First", "TEST-Last");
        h.Patients.Add(patient);
        h.Appointments.Add(NewAppointment(
            patient.Id, AppointmentStatusType.Pending, new DateTime(2026, 10, 5, 9, 0, 0), "TEST-P0001",
            dueDate: new DateTime(2026, 9, 28)));

        await h.Job.ExecuteAsync();

        var digest = h.Digests().ShouldHaveSingleItem();
        digest.TenantId.ShouldBe(OfficeId);
        var row = digest.Rows.ShouldHaveSingleItem();
        row.RequestConfirmationNumber.ShouldBe("TEST-P0001");
        row.PatientName.ShouldBe("TEST-First TEST-Last");
        row.AppointmentDate.ShouldBe(new DateTime(2026, 10, 5, 9, 0, 0));
        row.DueDate.ShouldBe(new DateTime(2026, 9, 28));
    }

    /// <summary>
    /// An office whose requests have all been decided gets NO digest -- not an empty one.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_NothingPending_PublishesNoDigest()
    {
        var h = new Harness();
        var patient = NewPatient("TEST-First", "TEST-Last");
        h.Patients.Add(patient);
        h.Appointments.Add(NewAppointment(patient.Id, AppointmentStatusType.Approved, new DateTime(2026, 10, 5), "TEST-P0002"));

        await h.Job.ExecuteAsync();

        h.Digests().ShouldBeEmpty("an office with no pending request must not receive an empty digest");
    }

    [Fact]
    public async Task ExecuteAsync_ListsOnlyPendingRequestsInAppointmentDateOrder()
    {
        var h = new Harness();
        var patient = NewPatient("TEST-First", "TEST-Last");
        h.Patients.Add(patient);
        h.Appointments.Add(NewAppointment(patient.Id, AppointmentStatusType.Pending, new DateTime(2026, 10, 20), "TEST-LATER"));
        h.Appointments.Add(NewAppointment(patient.Id, AppointmentStatusType.Approved, new DateTime(2026, 10, 1), "TEST-DECIDED"));
        h.Appointments.Add(NewAppointment(patient.Id, AppointmentStatusType.Pending, new DateTime(2026, 10, 3), "TEST-SOONER"));
        h.Appointments.Add(NewAppointment(patient.Id, AppointmentStatusType.CancelledNoBill, new DateTime(2026, 10, 2), "TEST-CANCELLED"));

        await h.Job.ExecuteAsync();

        h.Digests().ShouldHaveSingleItem().Rows.Select(r => r.RequestConfirmationNumber)
            .ShouldBe(new[] { "TEST-SOONER", "TEST-LATER" });
    }

    /// <summary>
    /// A request whose patient row cannot be found is still listed, as "(unnamed patient)", so the
    /// office never loses sight of it.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ARequestWhosePatientIsMissing_IsListedAsUnnamed()
    {
        var h = new Harness();
        h.Appointments.Add(NewAppointment(Guid.NewGuid(), AppointmentStatusType.Pending, new DateTime(2026, 10, 5), "TEST-P0003"));

        await h.Job.ExecuteAsync();

        h.Digests().ShouldHaveSingleItem().Rows.ShouldHaveSingleItem().PatientName.ShouldBe("(unnamed patient)");
    }
}
