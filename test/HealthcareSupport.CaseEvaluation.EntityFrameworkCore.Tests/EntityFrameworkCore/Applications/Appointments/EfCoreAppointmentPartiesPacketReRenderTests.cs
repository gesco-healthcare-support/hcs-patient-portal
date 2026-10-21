using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Jobs;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Pdf;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Templates;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Notifications.Outbox;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// The attorney and claim examiner packet is re-rendered when staff change those parties on a live
/// appointment (2026-10-21), end to end: the staff save, the queued re-render, the REAL render job, the
/// REAL packet email handler and the REAL notification outbox with its once-per-address rule.
///
/// <para>What is proven: the new party is mailed exactly once, with the NEW copy, and only after it
/// exists; a party already mailed is not mailed again; every earlier file stays in storage and is
/// recorded as an <see cref="AppointmentPacketVersion"/>; a re-render that cannot be produced still
/// mails the new party the packet as it stands.</para>
///
/// <para>Stand-ins, and why: the packet-renderer sidecar (a stub that returns numbered bytes, so each
/// render is distinguishable), the packets blob container (in memory, so files are checkable and an
/// overwrite would be caught), the token resolver (the stub's output does not depend on tokens) and the
/// background job queue (recorded, so a test runs the queued re-render itself, in order). Everything
/// between them is the production code. The office's mail templates are seeded so the real dispatcher
/// can render. Values are synthetic.</para>
/// </summary>
public class EfCoreAppointmentPartiesPacketReRenderTests
    : CaseEvaluationApplicationTestBase<CaseEvaluationEntityFrameworkCoreTestModule>
{
    private static readonly Guid BookerId = IdentityUsersTestData.ApplicantAttorney1UserId;

    private readonly MemoryPacketContainer _files = new();
    private readonly IHtmlPacketRenderer _renderer = Substitute.For<IHtmlPacketRenderer>();
    private readonly IBackgroundJobManager _jobs = Substitute.For<IBackgroundJobManager>();
    private int _renders;

    private readonly IAppointmentPartiesAppService _parties;
    private readonly GenerateAppointmentPacketJob _packetJob;
    private readonly IPacketAttachmentProvider _attachments;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IRepository<AppointmentPacket, Guid> _packets;
    private readonly IRepository<AppointmentPacketVersion, Guid> _versions;
    private readonly IRepository<NotificationOutboxItem, Guid> _mailOutbox;
    private readonly INotificationTemplateRepository _templates;
    private readonly INotificationTemplateTypeRepository _templateTypes;
    private readonly ICurrentTenant _currentTenant;

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<IBlobContainer<AppointmentPacketsContainer>>(_files));
        services.Replace(ServiceDescriptor.Singleton(_renderer));
        services.Replace(ServiceDescriptor.Singleton(_jobs));
        var tokens = Substitute.For<IPacketTokenResolver>();
        tokens.ResolveAsync(Arg.Any<Guid>()).Returns(new PacketTokenContext { AppointmentType = "TEST AME" });
        services.Replace(ServiceDescriptor.Singleton(tokens));
    }

    public EfCoreAppointmentPartiesPacketReRenderTests()
    {
        _renderer.RenderAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => Encoding.ASCII.GetBytes($"%PDF-TEST-render-{Interlocked.Increment(ref _renders)}"));

        _parties = GetRequiredService<IAppointmentPartiesAppService>();
        _packetJob = GetRequiredService<GenerateAppointmentPacketJob>();
        _attachments = GetRequiredService<IPacketAttachmentProvider>();
        _appointmentRepository = GetRequiredService<IAppointmentRepository>();
        _patientRepository = GetRequiredService<IRepository<Patient, Guid>>();
        _packets = GetRequiredService<IRepository<AppointmentPacket, Guid>>();
        _versions = GetRequiredService<IRepository<AppointmentPacketVersion, Guid>>();
        _mailOutbox = GetRequiredService<IRepository<NotificationOutboxItem, Guid>>();
        _templates = GetRequiredService<INotificationTemplateRepository>();
        _templateTypes = GetRequiredService<INotificationTemplateTypeRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task A_new_party_is_mailed_the_new_copy_once_and_nobody_already_mailed_is_mailed_again()
    {
        var token = NewToken();
        var defense = $"da-rr-{token}@example.test";
        var applicant = $"aa-rr-{token}@example.test";
        var id = await CreateApprovedAppointmentWithPacketsAsync(token);
        var packetId = await AttorneyPacketIdAsync(id);

        // 1. Staff add a defense attorney. Nothing is mailed until the re-render has produced the new copy.
        await AsStaffAsync(() => _parties.UpdateAsync(id, new AppointmentPartiesUpdateDto { DefenseAttorney = Attorney(defense) }));
        (await AttorneyPacketMailAsync(id)).ShouldBeEmpty("nobody is sent the old copy first");
        await RunQueuedReRendersAsync(id, expected: 1);

        var afterFirst = await PacketAsync(packetId);
        (await AttorneyPacketMailAsync(id)).Select(m => m.To).ShouldBe(new[] { defense });
        (await AttachmentTextAsync(id)).ShouldBe("%PDF-TEST-render-1", "the new attorney's mail carries the re-rendered file");

        // 2. Staff add an applicant attorney: only the new address is mailed.
        await AsStaffAsync(() => _parties.UpdateAsync(id, new AppointmentPartiesUpdateDto { ApplicantAttorney = Attorney(applicant) }));
        await RunQueuedReRendersAsync(id, expected: 1);

        var mail = await AttorneyPacketMailAsync(id);
        mail.Count(m => m.To == defense).ShouldBe(1, "the defense attorney was mailed once, at their own addition");
        mail.Count(m => m.To == applicant).ShouldBe(1);
        mail.ShouldAllBe(m => m.PacketId == packetId);
        (await AttachmentTextAsync(id)).ShouldBe("%PDF-TEST-render-2");

        // 3. Every file is kept, and each replaced one is recorded.
        var latest = await PacketAsync(packetId);
        latest.Status.ShouldBe(PacketGenerationStatus.Generated);
        var versions = (await InOfficeAsync(() => _versions.GetListAsync(v => v.AppointmentPacketId == packetId)))
            .OrderBy(v => v.CreationTime).ToList();
        versions.Select(v => v.BlobName).ShouldBe(new[] { OriginalBlob(id), afterFirst.BlobName });
        versions.ShouldAllBe(v => v.Kind == PacketKind.AttorneyClaimExaminer && v.AppointmentId == id);
        _files.Text(OriginalBlob(id)).ShouldBe("%PDF-TEST-approval", "the file mailed at approval is never overwritten or deleted");
        _files.Text(afterFirst.BlobName).ShouldBe("%PDF-TEST-render-1");
        _files.Text(latest.BlobName).ShouldBe("%PDF-TEST-render-2");
    }

    [Fact]
    public async Task A_patient_email_correction_re_renders_nothing_and_mails_the_patient_packet_as_it_stands()
    {
        var token = NewToken();
        var newEmail = $"pt-rr-{token}@example.test";
        var id = await CreateApprovedAppointmentWithPacketsAsync(token);
        // The staff save corrects the Patient record first (the existing patient update), then the
        // appointment's own column through the parties call. Mirror that order.
        await InOfficeAsync(async () =>
        {
            var appointment = await _appointmentRepository.GetAsync(id);
            var patient = await _patientRepository.GetAsync(appointment.PatientId);
            patient.Email = newEmail;
            await _patientRepository.UpdateAsync(patient, autoSave: true);
        });

        await AsStaffAsync(() => _parties.UpdateAsync(id, new AppointmentPartiesUpdateDto { PatientEmail = newEmail }));

        QueuedReRenders(id).ShouldBeEmpty();
        var rows = await InOfficeAsync(() => _mailOutbox.GetListAsync(r => r.PacketAppointmentId == id));
        rows.ShouldHaveSingleItem().To.ShouldBe(newEmail);
        rows[0].PacketKind.ShouldBe(PacketKind.Patient);
        _renders.ShouldBe(0, "no packet was rendered again");
    }

    [Fact]
    public async Task A_re_render_that_cannot_be_produced_still_mails_the_new_party_the_packet_as_it_stands()
    {
        var token = NewToken();
        var defense = $"da-rrfail-{token}@example.test";
        var id = await CreateApprovedAppointmentWithPacketsAsync(token);
        var packetId = await AttorneyPacketIdAsync(id);
        _renderer.RenderAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("TEST template failure"));

        await AsStaffAsync(() => _parties.UpdateAsync(id, new AppointmentPartiesUpdateDto { DefenseAttorney = Attorney(defense) }));
        await RunQueuedReRendersAsync(id, expected: 1);

        (await AttorneyPacketMailAsync(id)).Select(m => m.To).ShouldBe(new[] { defense });
        var packet = await PacketAsync(packetId);
        packet.Status.ShouldBe(PacketGenerationStatus.Generated, "the packet in place is still good and is not marked failed");
        packet.BlobName.ShouldBe(OriginalBlob(id));
        (await InOfficeAsync(() => _versions.GetListAsync(v => v.AppointmentPacketId == packetId))).ShouldBeEmpty();
    }

    // ----------------------------------------------------------------- helpers

    private static string NewToken() => Guid.NewGuid().ToString("N")[..8];

    private static string OriginalBlob(Guid id) => $"{TenantsTestData.TenantARef}/{id}/packet/attorneyclaimexaminer/approval.pdf";

    private static StaffAttorneyChangeDto Attorney(string email) => new()
    {
        Email = email,
        FirstName = "TEST-First",
        LastName = "TEST-Last",
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

    private async Task<T> InOfficeAsync<T>(Func<Task<T>> action) =>
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                return await action();
            }
        });

    private Task InOfficeAsync(Func<Task> action) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await action();
            }
        });

    private List<GenerateAppointmentPacketArgs> QueuedReRenders(Guid id) =>
        _jobs.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IBackgroundJobManager.EnqueueAsync))
            .Select(c => c.GetArguments()[0])
            .OfType<GenerateAppointmentPacketArgs>()
            .Where(a => a.AppointmentId == id)
            .ToList();

    private int _ran;

    /// <summary>Runs the re-renders queued since the last call, through the real job, as the worker would.</summary>
    private async Task RunQueuedReRendersAsync(Guid id, int expected)
    {
        var fresh = QueuedReRenders(id).Skip(_ran).ToList();
        fresh.Count.ShouldBe(expected);
        foreach (var args in fresh)
        {
            args.Kind.ShouldBe(PacketKind.AttorneyClaimExaminer);
            args.ReRender.ShouldBeTrue();
            args.TenantId.ShouldBe(TenantsTestData.TenantARef);
            await _packetJob.ExecuteAsync(args);
        }
        _ran += fresh.Count;
    }

    private Task<List<NotificationOutboxItem>> AttorneyPacketMailAsync(Guid id) =>
        InOfficeAsync(() => _mailOutbox.GetListAsync(r =>
            r.PacketAppointmentId == id && r.PacketKind == PacketKind.AttorneyClaimExaminer));

    private Task<AppointmentPacket> PacketAsync(Guid packetId) => InOfficeAsync(() => _packets.GetAsync(packetId));

    private async Task<Guid> AttorneyPacketIdAsync(Guid id) =>
        (await InOfficeAsync(() => _packets.GetListAsync(p => p.AppointmentId == id && p.Kind == PacketKind.AttorneyClaimExaminer))).Single().Id;

    /// <summary>The bytes the mail drain would attach right now, read the way it reads them.</summary>
    private Task<string> AttachmentTextAsync(Guid id) =>
        InOfficeAsync(async () =>
        {
            var attachment = await _attachments.GetAttachmentAsync(id, PacketKind.AttorneyClaimExaminer);
            return Encoding.ASCII.GetString(attachment!.Bytes);
        });

    private async Task<Guid> CreateApprovedAppointmentWithPacketsAsync(string token)
    {
        var appointmentId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var email = $"pt-{token}@example.test";
        await InOfficeAsync(async () =>
        {
            await SeedTemplatesAsync();
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
                identityUserId: BookerId,
                appointmentTypeId: LocationsTestData.AppointmentType1Id,
                locationId: LocationsTestData.Location1Id,
                doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot1Id,
                appointmentDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                requestConfirmationNumber: $"A9-RR-{token}",
                appointmentStatus: AppointmentStatusType.Approved)
            {
                TenantId = TenantsTestData.TenantARef,
                PatientEmail = email,
            };
            appointment.RecordBookedBy(BookerId);
            await _appointmentRepository.InsertAsync(appointment, autoSave: true);

            // The three packets as approval left them, each with its file in storage.
            foreach (var kind in new[] { PacketKind.Patient, PacketKind.Doctor, PacketKind.AttorneyClaimExaminer })
            {
                var blob = kind == PacketKind.AttorneyClaimExaminer
                    ? OriginalBlob(appointmentId)
                    : $"{TenantsTestData.TenantARef}/{appointmentId}/packet/{kind.ToString().ToLowerInvariant()}/approval.pdf";
                await _files.SaveAsync(blob, new MemoryStream(Encoding.ASCII.GetBytes("%PDF-TEST-approval")));
                await _packets.InsertAsync(new AppointmentPacket(
                    Guid.NewGuid(), TenantsTestData.TenantARef, appointmentId, kind, blob, PacketGenerationStatus.Generated), autoSave: true);
            }
        });
        return appointmentId;
    }

    private async Task SeedTemplatesAsync()
    {
        // The collection shares one database, so reuse a type another class left in this office.
        var typeId = (await _templateTypes.GetListAsync()).FirstOrDefault()?.Id
            ?? (await _templateTypes.InsertAsync(new NotificationTemplateType(Guid.NewGuid(), "Email"), autoSave: true)).Id;

        var existing = (await _templates.GetQueryableAsync()).Select(x => x.TemplateCode).ToList();
        foreach (var code in NotificationTemplateConsts.Codes.All.Where(c => !existing.Contains(c)))
        {
            await _templates.InsertAsync(new NotificationTemplate(
                id: Guid.NewGuid(),
                tenantId: TenantsTestData.TenantARef,
                templateCode: code,
                templateTypeId: typeId,
                subject: $"[{code}] -- TEST",
                bodyEmail: $"<p>Stub for {code}</p>",
                bodySms: $"Stub for {code}",
                description: null,
                isActive: true), autoSave: true);
        }
    }

    /// <summary>The packets container in memory. Saving over an existing key without overrideExisting throws,
    /// as the real providers do, so an accidental overwrite of a kept file fails the test.</summary>
    private sealed class MemoryPacketContainer : IBlobContainer<AppointmentPacketsContainer>
    {
        private readonly ConcurrentDictionary<string, byte[]> _blobs = new(StringComparer.Ordinal);

        public string Text(string name) => _blobs.TryGetValue(name, out var b) ? Encoding.ASCII.GetString(b) : "(missing)";

        public async Task SaveAsync(string name, Stream stream, bool overrideExisting = false, CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            if (!overrideExisting && _blobs.ContainsKey(name))
            {
                throw new BlobAlreadyExistsException($"TEST: {name} already exists");
            }
            _blobs[name] = ms.ToArray();
        }

        public Task<bool> DeleteAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(_blobs.TryRemove(name, out _));

        public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(_blobs.ContainsKey(name));

        public async Task<Stream> GetAsync(string name, CancellationToken cancellationToken = default) =>
            await GetOrNullAsync(name, cancellationToken) ?? throw new AbpException($"TEST: no blob {name}");

        public Task<Stream?> GetOrNullAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream?>(_blobs.TryGetValue(name, out var b) ? new MemoryStream(b) : null);
    }
}
