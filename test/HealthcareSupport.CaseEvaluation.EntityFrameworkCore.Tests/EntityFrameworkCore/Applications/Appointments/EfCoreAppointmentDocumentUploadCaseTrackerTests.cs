using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications.Delivery;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Shouldly;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// A STAFF upload on an approved, settled appointment must reach the Case Tracker (2026-11-15).
///
/// <para>A staff upload lands Accepted and raises no document-accepted event, so the Case Tracker
/// intake (whose payload carries the document list) is queued by the appointment-changed handler.
/// That handler used to be reached only through ABP's navigation event for the new document row;
/// the upload method no longer tracks the appointment (to avoid the concurrency conflict between
/// parallel uploads), so the service re-pushes explicitly. This test pins the outcome.</para>
/// </summary>
public class EfCoreAppointmentDocumentUploadCaseTrackerTests
    : CaseEvaluationApplicationTestBase<CaseEvaluationEntityFrameworkCoreTestModule>
{
    private readonly IAppointmentDocumentsAppService _documents;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IRepository<AppointmentPacket, Guid> _packets;
    private readonly IIntegrationOutboxRepository _outbox;
    private readonly ICurrentTenant _currentTenant;

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<INotificationDispatcher>(new NotificationRecorder()));
        services.Replace(ServiceDescriptor.Singleton(Substitute.For<IBlobContainer<AppointmentDocumentsContainer>>()));
    }

    public EfCoreAppointmentDocumentUploadCaseTrackerTests()
    {
        _documents = GetRequiredService<IAppointmentDocumentsAppService>();
        _appointmentRepository = GetRequiredService<IAppointmentRepository>();
        _patientRepository = GetRequiredService<IRepository<Patient, Guid>>();
        _packets = GetRequiredService<IRepository<AppointmentPacket, Guid>>();
        _outbox = GetRequiredService<IIntegrationOutboxRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task A_staff_upload_on_an_approved_settled_appointment_queues_an_intake_listing_the_document()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var id = await CreateAppointmentAsync(token);
        await SeedSettledPacketsAsync(id);
        var fileName = $"TEST-doc-{token}.pdf";

        var bytes = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 1 0 obj<<>>endobj trailer<<>> %%EOF");
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            using var stream = new System.IO.MemoryStream(bytes);
            await _documents.UploadStreamAsync(id, fileName, fileName, "application/pdf", bytes.Length, stream);
        }

        var intakes = await InOfficeAsync(async () =>
            (await _outbox.GetListAsync()).Where(r => r.AppointmentId == id && r.MessageType == IntegrationMessageType.Intake).ToList());
        intakes.ShouldNotBeEmpty("the upload must reach the Case Tracker");
        intakes.ShouldContain(r => r.Payload.Contains(fileName, StringComparison.Ordinal),
            "the queued intake lists the new document");
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
        string token)
    {
        var appointmentId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var booker = IdentityUsersTestData.ApplicantAttorney1UserId;
        var email = $"pt-{token}@example.test";
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
                appointmentStatus: AppointmentStatusType.Approved)
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
