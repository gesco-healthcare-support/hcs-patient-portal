using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.Patients;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Builds the intake payload with the REAL resolvers over mocked repositories, so the assertions
/// cover actual contract fidelity rather than a mock's opinion of it. All fixture data is synthetic.
///
/// <para>The most valuable assertion here is the negative one: the serialized JSON must contain no
/// patient identifier. The portal has no cross-office patient identity and CalMed mints a new id per
/// claim, so publishing ours would hand the receiver something that looks authoritative and is not.</para>
/// </summary>
public class IntakePayloadBuilderTests
{
    private const string SyntheticPanelNumber = "PN-SAMPLE";

    private static readonly Guid TenantId = new("b8844bba-414c-e238-4a71-3a22841f21af");
    private static readonly Guid AppointmentId = new("ada5e3c5-0034-ebde-253c-3a2293631dee");
    private static readonly Guid SourceAppointmentId = new("3c9d1b77-2e40-4a51-8bb2-77f0a1c9d233");
    private static readonly Guid AppointmentTypeId = new("a1c2e3f4-5566-4778-9900-aabbccddeeff");
    private static readonly Guid LocationId = new("c0ffee0a-bcde-4f01-9abc-de0123456f7a");
    private static readonly Guid SlotId = new("d1e2f3a4-b5c6-4d7e-8f90-a1b2c3d4e5fa");
    private static readonly Guid PatientId = new("e5f6a7b8-c9d0-4e1f-a2b3-c4d5e6f7a8bc");

    private static Appointment NewAppointment(
        Guid id,
        string confirmation,
        EvaluationKind kind = EvaluationKind.Evaluation,
        Guid? originalAppointmentId = null)
    {
        return new Appointment(
            id, PatientId, identityUserId: null, AppointmentTypeId, LocationId, SlotId,
            appointmentDate: new DateTime(2026, 8, 15, 9, 30, 0, DateTimeKind.Utc),
            requestConfirmationNumber: confirmation,
            appointmentStatus: AppointmentStatusType.Approved,
            panelNumber: SyntheticPanelNumber)
        {
            TenantId = TenantId,
            EvaluationKind = kind,
            OriginalAppointmentId = originalAppointmentId,
            AppointmentApproveDate = new DateTime(2026, 7, 27, 18, 30, 12, DateTimeKind.Utc),
        };
    }

    private static IntakePayloadBuilder Build(
        Appointment appointment,
        Appointment? sourceAppointment = null,
        List<AppointmentDocument>? documents = null,
        List<AppointmentPacket>? packets = null)
    {
        var appointmentRepo = Substitute.For<IRepository<Appointment, Guid>>();
        appointmentRepo.GetAsync(appointment.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(appointment));
        appointmentRepo.FindAsync(SourceAppointmentId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(sourceAppointment));

        var slotRepo = Substitute.For<IRepository<DoctorAvailability, Guid>>();
        slotRepo.FindAsync(SlotId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DoctorAvailability?>(new DoctorAvailability(
                SlotId, LocationId,
                availableDate: new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
                fromTime: new TimeOnly(9, 30),
                toTime: new TimeOnly(10, 30),
                bookingStatusId: BookingStatus.Available)));

        var typeRepo = Substitute.For<IRepository<AppointmentType, Guid>>();
        typeRepo.FindAsync(AppointmentTypeId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AppointmentType?>(new AppointmentType(
                AppointmentTypeId, "Panel Qualified Medical Examination (PQME)")));

        var patientRepo = Substitute.For<IRepository<Patient, Guid>>();
        patientRepo.FindAsync(PatientId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Patient?>(new Patient(
                PatientId, stateId: null, appointmentLanguageId: null, identityUserId: null,
                tenantId: TenantId,
                firstName: "Jordan", lastName: "Sample", email: "jordan.sample@example.test",
                genderId: Gender.Male,
                dateOfBirth: new DateTime(1985, 4, 12, 0, 0, 0, DateTimeKind.Utc),
                phoneNumberTypeId: PhoneNumberType.Home,
                middleName: "A", phoneNumber: "555-0142", cellPhoneNumber: "555-0177")));

        var doctors = new List<Doctor>
        {
            new(Guid.NewGuid(), "Morgan", "Reyes", "morgan.reyes@example.test", Gender.Male),
        };
        var doctorRepo = Substitute.For<IRepository<Doctor, Guid>>();
        doctorRepo.GetQueryableAsync().Returns(_ => doctors.AsQueryable());

        var locationRepo = Substitute.For<IRepository<Location, Guid>>();
        locationRepo.FindAsync(LocationId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Location?>(new Location(
                LocationId, stateId: null, name: "North Clinic", parkingFee: 10m, isActive: true,
                address: "120 Example Blvd", city: "Encino", zipCode: "91316", facilityId: "FAC-SAMPLE")));

        var tenantStore = Substitute.For<ITenantStore>();
        tenantStore.FindAsync(TenantId)
            .Returns(Task.FromResult<TenantConfiguration?>(new TenantConfiguration(TenantId, "Reyes Medical Group")));

        var configuration = Substitute.For<IConfiguration>();
        configuration["BlobStoring:Minio:BucketName"].Returns("case-evaluation-documents");

        var documentRepo = Substitute.For<IRepository<AppointmentDocument, Guid>>();
        documentRepo.GetListAsync(
                Arg.Any<Expression<Func<AppointmentDocument, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(documents ?? new List<AppointmentDocument>()));

        var packetRepo = Substitute.For<IRepository<AppointmentPacket, Guid>>();
        packetRepo.GetListAsync(
                Arg.Any<Expression<Func<AppointmentPacket, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(packets ?? new List<AppointmentPacket>()));

        var documentTypeRepo = Substitute.For<IRepository<AppointmentDocumentType, Guid>>();
        documentTypeRepo.GetListAsync(
                Arg.Any<Expression<Func<AppointmentDocumentType, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<AppointmentDocumentType>()));

        return new IntakePayloadBuilder(
            appointmentRepo,
            new AppointmentCoreResolver(appointmentRepo, slotRepo, typeRepo),
            new PartyResolver(patientRepo, doctorRepo),
            new TenantLocationResolver(locationRepo, tenantStore, configuration),
            new DocumentListResolver(documentRepo, packetRepo, documentTypeRepo),
            SimpleGuidGenerator.Instance);
    }

    [Fact]
    public async Task BuildAsync_PopulatesTheContractFields()
    {
        var builder = Build(NewAppointment(AppointmentId, "A00065"));

        var envelope = await builder.BuildAsync(AppointmentId);
        var data = envelope.Data;

        data.AppointmentId.ShouldBe(AppointmentId);
        data.ConfirmationNumber.ShouldBe("A00065");
        data.Status.ShouldBe("Approved");
        data.PanelNumber.ShouldBe(SyntheticPanelNumber);
        data.ApprovedAtUtc.ShouldNotBeNull();
        data.ApprovedAtUtc!.ShouldEndWith("Z");
        data.Tenant.FacilityId.ShouldBe("FAC-SAMPLE");
        data.Tenant.OfficeName.ShouldBe("Reyes Medical Group");
        data.Location.Name.ShouldBe("North Clinic");
        data.AppointmentType.Name.ShouldBe("Panel Qualified Medical Examination (PQME)");
        data.Doctor.FirstName.ShouldBe("Morgan");
        data.Doctor.LastName.ShouldBe("Reyes");
        data.Storage.Bucket.ShouldBe("case-evaluation-documents");
        envelope.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task BuildAsync_DerivesScheduleFromTheSlot()
    {
        var builder = Build(NewAppointment(AppointmentId, "A00065"));

        var data = (await builder.BuildAsync(AppointmentId)).Data;

        data.AppointmentDateLocal.ShouldBe("2026-08-15");
        data.AppointmentTimeLocal.ShouldBe("09:30");
        data.TimeZone.ShouldBe("America/Los_Angeles");
        data.DurationMinutes.ShouldBe(60); // 09:30 -> 10:30, derived (no stored duration)
    }

    [Fact]
    public async Task BuildAsync_FormatsPatientFieldsForTheWire()
    {
        var builder = Build(NewAppointment(AppointmentId, "A00065"));

        var patient = (await builder.BuildAsync(AppointmentId)).Data.Patient;

        patient.FirstName.ShouldBe("Jordan");
        patient.MiddleName.ShouldBe("A");
        patient.LastName.ShouldBe("Sample");
        patient.DateOfBirth.ShouldBe("1985-04-12"); // date only, no time component
        patient.PhoneNumberType.ShouldBe("Home");
        patient.CellPhoneNumber.ShouldBe("555-0177");
    }

    [Fact]
    public async Task BuildAsync_ForAFirstEvaluation_SendsEvalAndNoPreviousLinks()
    {
        var builder = Build(NewAppointment(AppointmentId, "A00065"));

        var data = (await builder.BuildAsync(AppointmentId)).Data;

        data.EvaluationKind.ShouldBe("EVAL");
        data.PreviousAppointmentId.ShouldBeNull();
        data.PreviousConfirmationNumber.ShouldBeNull();
    }

    [Fact]
    public async Task BuildAsync_ForAReEvaluation_LinksBackToTheOriginal()
    {
        var reval = NewAppointment(
            AppointmentId, "A00065", EvaluationKind.ReEvaluation, SourceAppointmentId);
        var source = NewAppointment(SourceAppointmentId, "A00041");
        var builder = Build(reval, sourceAppointment: source);

        var data = (await builder.BuildAsync(AppointmentId)).Data;

        data.EvaluationKind.ShouldBe("RE_EVAL");
        data.PreviousAppointmentId.ShouldBe(SourceAppointmentId);
        // Display aid only -- the re-eval has its own fresh confirmation number.
        data.PreviousConfirmationNumber.ShouldBe("A00041");
    }

    [Fact]
    public async Task BuildAsync_OmitsNonFetchableDocuments()
    {
        var queued = AppointmentDocument.CreateQueued(
            Guid.NewGuid(), TenantId, AppointmentId, "Consent form", Guid.NewGuid());
        var uploaded = new AppointmentDocument(
            Guid.NewGuid(), TenantId, AppointmentId, "Medical records", "records.pdf",
            blobName: "tenantseg/apptseg/f97796c9365b4ad3a16408f72981cae3",
            contentType: "application/pdf", fileSize: 2048, uploadedByUserId: Guid.NewGuid())
        { Status = DocumentStatus.Accepted };

        var generating = new AppointmentPacket(
            Guid.NewGuid(), TenantId, AppointmentId, PacketKind.Doctor, "blob/doctor.pdf",
            PacketGenerationStatus.Generating);
        var generated = new AppointmentPacket(
            Guid.NewGuid(), TenantId, AppointmentId, PacketKind.Patient, "blob/patient.pdf",
            PacketGenerationStatus.Generated);

        var builder = Build(
            NewAppointment(AppointmentId, "A00065"),
            documents: new List<AppointmentDocument> { queued, uploaded },
            packets: new List<AppointmentPacket> { generating, generated });

        var documents = (await builder.BuildAsync(AppointmentId)).Data.Documents;

        // The queued placeholder and the still-rendering packet have no object to fetch.
        documents.Count.ShouldBe(2);
        documents.ShouldContain(d => d.Id == uploaded.Id);
        documents.ShouldContain(d => d.Id == generated.Id);
        documents.ShouldNotContain(d => d.Id == queued.Id);
        documents.ShouldNotContain(d => d.Id == generating.Id);
    }

    [Fact]
    public async Task BuildAsync_AtIntakeTime_HasAnEmptyDocumentList()
    {
        // Packets render asynchronously after approval, so the first push normally carries none.
        var builder = Build(NewAppointment(AppointmentId, "A00065"));

        (await builder.BuildAsync(AppointmentId)).Data.Documents.ShouldBeEmpty();
    }

    [Fact]
    public async Task SerializedPayload_IsCamelCase_AndCarriesNoPatientIdentifier()
    {
        var builder = Build(NewAppointment(AppointmentId, "A00065"));
        var envelope = await builder.BuildAsync(AppointmentId);

        var json = IntakePayloadSerializer.Serialize(envelope);

        json.ShouldContain("\"appointmentId\"");
        json.ShouldContain("\"evaluationKind\"");
        json.ShouldContain("\"dateOfBirth\"");
        json.ShouldContain("\"facilityId\"");
        // No patient identifier of any kind may appear -- see the class remarks.
        json.ShouldNotContain("\"patientId\"");
        json.ShouldNotContain("\"calMed\"");
    }
}
