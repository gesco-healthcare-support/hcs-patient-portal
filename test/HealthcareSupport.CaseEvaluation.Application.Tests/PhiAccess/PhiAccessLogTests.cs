using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Reports;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.PhiAccess;

/// <summary>
/// Every read that discloses PHI writes exactly one access-log row per record it disclosed, naming
/// the caller, the patient, the appointment and, for a download, the file. One test per endpoint,
/// so deleting any single <c>PhiAccessRecorder</c> call fails the test named for that endpoint
/// (each was checked by removing the call). Refused reads write nothing.
/// </summary>
/// <remarks>
/// Runs on the standard integration base (real SQLite, seeded office A, its patient 1 and
/// appointment 1). The two blob containers are replaced so a download returns a file without
/// storage. Each call runs inside an outer unit of work, as a request does in production, so the
/// recorder's own unit of work is exercised nested. All identifiers and bytes are synthetic.
/// </remarks>
public abstract class PhiAccessLogTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string Staff = "Staff Supervisor";
    private static readonly Guid StaffUserId = new("7f4a2b10-0000-4000-9000-000000000001");
    private static readonly Guid StrangerUserId = new("7f4a2b10-0000-4000-9000-000000000002");
    private static readonly Guid OfficeA = TenantsTestData.TenantARef;
    private static readonly Guid PatientId = PatientsTestData.Patient1Id;
    private static readonly Guid AppointmentId = AppointmentsTestData.Appointment1Id;

    private IBlobContainer<AppointmentDocumentsContainer> _documentBlobs = null!;
    private IBlobContainer<AppointmentPacketsContainer> _packetBlobs = null!;

    private readonly IPatientsAppService _patients;
    private readonly IAppointmentsAppService _appointments;
    private readonly IAppointmentDocumentsAppService _documents;
    private readonly IAppointmentPacketsAppService _packets;
    private readonly IReportsAppService _reports;
    private readonly IAppointmentDemographicsAppService _demographics;
    private readonly PhiAccessRecorder _recorder;
    private readonly IRepository<PhiAccessLogEntry, Guid> _log;
    private readonly IRepository<AppointmentDocument, Guid> _documentRepository;
    private readonly IRepository<AppointmentPacket, Guid> _packetRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principal;

    protected PhiAccessLogTests()
    {
        _patients = GetRequiredService<IPatientsAppService>();
        _appointments = GetRequiredService<IAppointmentsAppService>();
        _documents = GetRequiredService<IAppointmentDocumentsAppService>();
        _packets = GetRequiredService<IAppointmentPacketsAppService>();
        _reports = GetRequiredService<IReportsAppService>();
        _demographics = GetRequiredService<IAppointmentDemographicsAppService>();
        _recorder = GetRequiredService<PhiAccessRecorder>();
        _log = GetRequiredService<IRepository<PhiAccessLogEntry, Guid>>();
        _documentRepository = GetRequiredService<IRepository<AppointmentDocument, Guid>>();
        _packetRepository = GetRequiredService<IRepository<AppointmentPacket, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        _documentBlobs = Substitute.For<IBlobContainer<AppointmentDocumentsContainer>>();
        _packetBlobs = Substitute.For<IBlobContainer<AppointmentPacketsContainer>>();
        _documentBlobs.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => Pdf());
        _packetBlobs.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => Pdf());
        services.Replace(ServiceDescriptor.Singleton<IBlobContainer<AppointmentDocumentsContainer>>(_documentBlobs));
        services.Replace(ServiceDescriptor.Singleton<IBlobContainer<AppointmentPacketsContainer>>(_packetBlobs));
    }

    // ------------------------------------------------------------------ harness

    private static MemoryStream Pdf() => new(Encoding.ASCII.GetBytes("%PDF-1.7 synthetic"));

    private async Task<T> As<T>(Guid userId, string role, Func<Task<T>> call)
    {
        using (_currentTenant.Change(OfficeA))
        using (WithCurrentUser.Run(_principal, userId, role))
        {
            return await WithUnitOfWorkAsync(call);
        }
    }

    private Task<T> AsStaff<T>(Func<Task<T>> call) => As(StaffUserId, Staff, call);

    private async Task<List<PhiAccessLogEntry>> RowsAsync()
    {
        using (_currentTenant.Change(OfficeA))
        {
            return await WithUnitOfWorkAsync(() => _log.GetListAsync());
        }
    }

    /// <summary>Exactly one row, written by staff in office A, for the given action.</summary>
    private async Task<PhiAccessLogEntry> SingleRowAsync(PhiAccessAction action)
    {
        var rows = await RowsAsync();
        var row = rows.ShouldHaveSingleItem();
        row.Action.ShouldBe(action);
        row.UserId.ShouldBe(StaffUserId);
        row.TenantId.ShouldBe(OfficeA);
        row.ImpersonatorUserId.ShouldBeNull();
        return row;
    }

    private Task<AppointmentDocument> InsertDocumentAsync() =>
        AsStaff(() => _documentRepository.InsertAsync(
            new AppointmentDocument(Guid.NewGuid(), OfficeA, AppointmentId, "Synthetic intake",
                "synthetic-intake.pdf", "office-a/synthetic", "application/pdf", 10, StaffUserId),
            autoSave: true));

    private Task<AppointmentPacket> InsertPacketAsync(PacketKind kind) =>
        AsStaff(() => _packetRepository.InsertAsync(
            new AppointmentPacket(Guid.NewGuid(), OfficeA, AppointmentId, kind, $"p/{kind}.pdf",
                PacketGenerationStatus.Generated),
            autoSave: true));

    private static GetAppointmentReportInput AppointmentOneWindow() => new()
    {
        AppointmentDateMin = AppointmentsTestData.Appointment1Date.AddDays(-1),
        AppointmentDateMax = AppointmentsTestData.Appointment1Date.AddDays(1),
        MaxResultCount = 100,
    };

    // ------------------------------------------------------------------ patient records

    [Fact]
    public async Task Opening_a_patient_record_is_recorded()
    {
        await AsStaff(() => _patients.GetWithNavigationPropertiesAsync(PatientId));

        var row = await SingleRowAsync(PhiAccessAction.PatientViewed);
        row.PatientId.ShouldBe(PatientId);
        row.AppointmentId.ShouldBeNull();
        row.DocumentId.ShouldBeNull();
    }

    [Fact]
    public async Task Reading_a_patient_by_id_is_recorded()
    {
        await AsStaff(() => _patients.GetAsync(PatientId));

        (await SingleRowAsync(PhiAccessAction.PatientViewed)).PatientId.ShouldBe(PatientId);
    }

    [Fact]
    public async Task The_booking_read_of_a_patient_is_recorded()
    {
        await AsStaff(() => _patients.GetPatientForAppointmentBookingAsync(PatientId));

        (await SingleRowAsync(PhiAccessAction.PatientViewed)).PatientId.ShouldBe(PatientId);
    }

    [Fact]
    public async Task The_booking_lookup_by_email_is_recorded_only_when_it_finds_someone()
    {
        var missing = await AsStaff(() => _patients.GetPatientByEmailForAppointmentBookingAsync("TEST-nobody@test.local"));
        missing.ShouldBeNull();
        (await RowsAsync()).ShouldBeEmpty();

        await AsStaff(() => _patients.GetPatientByEmailForAppointmentBookingAsync(PatientsTestData.Patient1Email));

        (await SingleRowAsync(PhiAccessAction.PatientViewed)).PatientId.ShouldBe(PatientId);
    }

    [Fact]
    public async Task Revealing_an_ssn_is_recorded()
    {
        var reveal = await AsStaff(() => _patients.GetFullSsnAsync(PatientId));

        reveal.SocialSecurityNumber.ShouldBe(PatientsTestData.Patient1SocialSecurityNumber);
        (await SingleRowAsync(PhiAccessAction.PatientSsnRevealed)).PatientId.ShouldBe(PatientId);
    }

    // ------------------------------------------------------------------ appointments

    [Fact]
    public async Task Opening_an_appointment_records_its_patient()
    {
        await AsStaff(() => _appointments.GetWithNavigationPropertiesAsync(AppointmentId));

        var row = await SingleRowAsync(PhiAccessAction.AppointmentViewed);
        row.AppointmentId.ShouldBe(AppointmentId);
        row.PatientId.ShouldBe(PatientId);
    }

    [Fact]
    public async Task Finding_an_appointment_by_confirmation_number_is_recorded()
    {
        await AsStaff(() => _appointments.GetByConfirmationNumberAsync(AppointmentsTestData.Appointment1RequestConfirmationNumber));

        var row = await SingleRowAsync(PhiAccessAction.AppointmentViewed);
        row.AppointmentId.ShouldBe(AppointmentId);
        row.PatientId.ShouldBe(PatientId);
    }

    // ------------------------------------------------------------------ downloads

    [Fact]
    public async Task Downloading_a_document_records_the_file_its_appointment_and_its_patient()
    {
        var document = await InsertDocumentAsync();

        await AsStaff(() => _documents.DownloadAsync(document.Id));

        var row = await SingleRowAsync(PhiAccessAction.DocumentDownloaded);
        row.DocumentId.ShouldBe(document.Id);
        row.AppointmentId.ShouldBe(AppointmentId);
        row.PatientId.ShouldBe(PatientId, "the recorder fills the patient in from the appointment");
    }

    [Fact]
    public async Task Downloading_the_patient_packet_is_recorded()
    {
        var packet = await InsertPacketAsync(PacketKind.Patient);

        await AsStaff(() => _packets.DownloadAsync(AppointmentId));

        var row = await SingleRowAsync(PhiAccessAction.PacketDownloaded);
        row.DocumentId.ShouldBe(packet.Id);
        row.AppointmentId.ShouldBe(AppointmentId);
        row.PatientId.ShouldBe(PatientId);
    }

    [Fact]
    public async Task Downloading_a_packet_by_kind_is_recorded()
    {
        var packet = await InsertPacketAsync(PacketKind.Doctor);

        await AsStaff(() => _packets.DownloadByKindAsync(AppointmentId, PacketKind.Doctor));

        var row = await SingleRowAsync(PhiAccessAction.PacketDownloaded);
        row.DocumentId.ShouldBe(packet.Id);
        row.PatientId.ShouldBe(PatientId);
    }

    // ------------------------------------------------------------------ exports

    [Fact]
    public async Task The_pdf_report_export_records_each_appointment_in_it()
    {
        await AsStaff(() => _reports.GetReportPdfAsync(AppointmentOneWindow()));

        var row = await SingleRowAsync(PhiAccessAction.ReportPdfExported);
        row.AppointmentId.ShouldBe(AppointmentId);
        row.PatientId.ShouldBe(PatientId);
    }

    [Fact]
    public async Task The_csv_report_export_records_each_appointment_in_it()
    {
        await AsStaff(() => _reports.GetReportCsvAsync(AppointmentOneWindow()));

        var row = await SingleRowAsync(PhiAccessAction.ReportCsvExported);
        row.AppointmentId.ShouldBe(AppointmentId);
        row.PatientId.ShouldBe(PatientId);
    }

    [Fact]
    public async Task The_demographics_export_records_the_view_and_the_export()
    {
        await AsStaff(() => _demographics.GetPdfAsync(AppointmentId));

        var rows = await RowsAsync();
        rows.Select(r => r.Action).OrderBy(a => a).ShouldBe(new[]
        {
            PhiAccessAction.AppointmentViewed,
            PhiAccessAction.DemographicsExported,
        });
        rows.ShouldAllBe(r => r.AppointmentId == AppointmentId && r.PatientId == PatientId && r.UserId == StaffUserId);
    }

    // ------------------------------------------------------------------ refusals

    [Fact]
    public async Task A_refused_read_records_nothing()
    {
        var document = await InsertDocumentAsync();

        // A patient login that is not this patient's: the SSN reveal refuses on ownership.
        await Should.ThrowAsync<AbpAuthorizationException>(() =>
            As(StrangerUserId, "Patient", () => _patients.GetFullSsnAsync(PatientId)));
        // An attorney who is no party to appointment 1: the party gate refuses the view and the file.
        await Should.ThrowAsync<BusinessException>(() =>
            As(StrangerUserId, "Applicant Attorney", () => _appointments.GetWithNavigationPropertiesAsync(AppointmentId)));
        await Should.ThrowAsync<BusinessException>(() =>
            As(StrangerUserId, "Applicant Attorney", () => _documents.DownloadAsync(document.Id)));

        (await RowsAsync()).ShouldBeEmpty();
    }

    // ------------------------------------------------------------------ the recorder itself

    [Fact]
    public async Task The_rows_of_one_export_share_one_time_and_an_empty_export_writes_nothing()
    {
        var otherAppointment = Guid.NewGuid();
        await AsStaff(async () =>
        {
            await _recorder.RecordManyAsync(PhiAccessAction.ReportCsvExported, Array.Empty<PhiAccessTarget>());
            await _recorder.RecordManyAsync(PhiAccessAction.ReportCsvExported, new[]
            {
                new PhiAccessTarget(PatientId, AppointmentId),
                new PhiAccessTarget(null, otherAppointment),
                new PhiAccessTarget(PatientsTestData.Patient2Id, null),
            });
            return true;
        });

        var rows = await RowsAsync();
        rows.Count.ShouldBe(3);
        rows.Select(r => r.AccessedAt).Distinct().ShouldHaveSingleItem();
        rows.Single(r => r.AppointmentId == otherAppointment).PatientId
            .ShouldBeNull("an appointment the recorder cannot find leaves the patient empty rather than failing");
        rows.Single(r => r.AppointmentId == null).PatientId.ShouldBe(PatientsTestData.Patient2Id);
    }

    [Fact]
    public async Task An_impersonated_read_records_the_real_person_too()
    {
        var operatorUserId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AbpClaimTypes.UserId, StaffUserId.ToString()),
            new Claim(AbpClaimTypes.Role, Staff),
            new Claim(AbpClaimTypes.ImpersonatorUserId, operatorUserId.ToString()),
        }, "Test"));

        using (_currentTenant.Change(OfficeA))
        using (_principal.Change(principal))
        {
            await WithUnitOfWorkAsync(() => _patients.GetFullSsnAsync(PatientId));
        }

        var row = (await RowsAsync()).ShouldHaveSingleItem();
        row.UserId.ShouldBe(StaffUserId);
        row.ImpersonatorUserId.ShouldBe(operatorUserId);
    }
}
