using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Documents;
using HealthcareSupport.CaseEvaluation.PackageDetails;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Unit coverage for WHAT <see cref="PackageDocumentQueueHandler"/> queues when an appointment is
/// submitted. <c>PackageDocumentQueueHandlerTests</c> pins the idempotency skip; these pin the queueing.
///
/// <para><b>WHAT IS PINNED.</b> Every active document of every active package for the appointment's
/// type is queued once, named after its master document and tagged with the office's system
/// category. Inactive packages, packages of another type, inactive links and missing or inactive
/// master documents are skipped without stopping the rest; with no system category the rows are
/// queued untagged rather than not at all.</para>
///
/// <para><b>The result asserted is the set of rows queued</b> through a substituted
/// <see cref="AppointmentDocumentManager"/> (constructed the way the existing test does; its
/// <c>CreateQueuedAsync</c> is virtual), so nothing is written. The repositories return in-memory
/// lists. The first Fact is the positive control for every "queues nothing" Fact. Synthetic data only.</para>
/// </summary>
public class PackageDocumentQueueHandlerQueueingTests
{
    private static readonly Guid TypeId = new("88888888-8888-8888-8888-888888888888");

    private sealed record Queued(Guid AppointmentId, string DocumentName, Guid? SourceDocumentId, Guid? TypeId);

    private sealed class Rig
    {
        public List<PackageDetail> Packages { get; } = new();
        public List<Document> Documents { get; } = new();
        public List<AppointmentDocumentType> Types { get; } = new();
        public AppointmentDocumentManager Manager { get; } =
            Substitute.For<AppointmentDocumentManager>(Substitute.For<IRepository<AppointmentDocument, Guid>>());

        public PackageDocumentQueueHandler Build()
        {
            var packages = Substitute.For<IPackageDetailRepository>();
            packages.GetQueryableAsync().Returns(_ => Packages.AsQueryable());
            var documents = Substitute.For<IRepository<Document, Guid>>();
            documents.GetQueryableAsync().Returns(_ => Documents.AsQueryable());
            var appointmentDocuments = Substitute.For<IRepository<AppointmentDocument, Guid>>();
            appointmentDocuments.GetQueryableAsync().Returns(_ => new List<AppointmentDocument>().AsQueryable());
            var types = Substitute.For<IAppointmentDocumentTypeRepository>();
            types.GetQueryableAsync().Returns(_ => Types.AsQueryable());
            var currentTenant = Substitute.For<ICurrentTenant>();
            currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(Substitute.For<IDisposable>());
            return new PackageDocumentQueueHandler(
                packages,
                documents,
                appointmentDocuments,
                types,
                Manager,
                currentTenant,
                NullLogger<PackageDocumentQueueHandler>.Instance);
        }

        public List<Queued> QueuedRows() =>
            Manager.ReceivedCalls()
                .Where(c => c.GetMethodInfo().Name == nameof(AppointmentDocumentManager.CreateQueuedAsync))
                .Select(c => c.GetArguments())
                .Select(a => new Queued((Guid)a[1]!, (string)a[2]!, (Guid?)a[3], (Guid?)a[4]))
                .ToList();

        public Document AddDocument(string name, bool isActive = true)
        {
            var document = new Document(Guid.NewGuid(), null, name, $"TEST-blob-{name}", "application/pdf", isActive);
            Documents.Add(document);
            return document;
        }

        public PackageDetail AddPackage(string name, Guid? appointmentTypeId, bool isActive, params (Document Document, bool LinkActive)[] links)
        {
            var package = new PackageDetail(Guid.NewGuid(), null, name, appointmentTypeId, isActive);
            foreach (var (document, linkActive) in links)
            {
                package.DocumentPackages.Add(new DocumentPackage(package.Id, document.Id, linkActive));
            }
            Packages.Add(package);
            return package;
        }
    }

    private static AppointmentSubmittedEto Submitted() => new()
    {
        AppointmentId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        AppointmentTypeId = TypeId,
        RequestConfirmationNumber = "TEST-PQ0001",
        AppointmentDate = new DateTime(2026, 11, 2, 9, 0, 0),
    };

    /// <summary>
    /// <b>POSITIVE CONTROL.</b> An active package with two active documents queues both, named after
    /// their master documents and tagged with the office's system category.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_AnActivePackage_QueuesEachActiveDocumentTagged_PositiveControl()
    {
        var rig = new Rig();
        var systemType = new AppointmentDocumentType(Guid.NewGuid(), "TEST-Generated Packet", isSystem: true);
        rig.Types.Add(systemType);
        var intake = rig.AddDocument("TEST-Intake Form");
        var consent = rig.AddDocument("TEST-Consent Form");
        rig.AddPackage("TEST-Package", TypeId, isActive: true, (intake, true), (consent, true));
        var evt = Submitted();

        await rig.Build().HandleEventAsync(evt);

        var rows = rig.QueuedRows();
        rows.Select(r => r.DocumentName).ShouldBe(new[] { "TEST-Intake Form", "TEST-Consent Form" }, ignoreOrder: true);
        rows.ShouldAllBe(r => r.AppointmentId == evt.AppointmentId && r.TypeId == systemType.Id);
        rows.Select(r => r.SourceDocumentId).ShouldBe(new Guid?[] { intake.Id, consent.Id }, ignoreOrder: true);
    }

    [Fact]
    public async Task HandleEventAsync_NullEvent_QueuesNothing()
    {
        var rig = new Rig();
        rig.AddPackage("TEST-Package", TypeId, isActive: true, (rig.AddDocument("TEST-Form"), true));

        await rig.Build().HandleEventAsync(null!);

        rig.QueuedRows().ShouldBeEmpty();
    }

    /// <summary>
    /// Only ACTIVE packages for THIS appointment type count: an inactive package, or one for another
    /// type, queues nothing.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_OnlyInactiveOrOtherTypePackages_QueuesNothing()
    {
        var rig = new Rig();
        rig.AddPackage("TEST-Inactive", TypeId, isActive: false, (rig.AddDocument("TEST-Form-A"), true));
        rig.AddPackage("TEST-OtherType", Guid.NewGuid(), isActive: true, (rig.AddDocument("TEST-Form-B"), true));

        await rig.Build().HandleEventAsync(Submitted());

        rig.QueuedRows().ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleEventAsync_APackageWhoseLinksAreAllInactive_QueuesNothing()
    {
        var rig = new Rig();
        rig.AddPackage("TEST-Package", TypeId, isActive: true, (rig.AddDocument("TEST-Form"), false));

        await rig.Build().HandleEventAsync(Submitted());

        rig.QueuedRows().ShouldBeEmpty();
    }

    /// <summary>
    /// A linked master document that is inactive (or gone) is skipped, and the package's other documents
    /// are still queued.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_AnInactiveMasterDocument_IsSkippedWithoutStoppingTheRest()
    {
        var rig = new Rig();
        var retired = rig.AddDocument("TEST-Retired Form", isActive: false);
        var missing = new Document(Guid.NewGuid(), null, "TEST-Never Stored", "TEST-blob-x", "application/pdf");
        var live = rig.AddDocument("TEST-Live Form");
        rig.AddPackage("TEST-Package", TypeId, isActive: true, (retired, true), (missing, true), (live, true));

        await rig.Build().HandleEventAsync(Submitted());

        rig.QueuedRows().ShouldHaveSingleItem().DocumentName.ShouldBe("TEST-Live Form");
    }

    /// <summary>
    /// An office missing its system document category still gets its documents queued -- untagged --
    /// rather than losing the whole queue.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_NoSystemCategory_QueuesTheRowsUntagged()
    {
        var rig = new Rig();
        rig.Types.Add(new AppointmentDocumentType(Guid.NewGuid(), "TEST-Ordinary Category", isSystem: false));
        rig.AddPackage("TEST-Package", TypeId, isActive: true, (rig.AddDocument("TEST-Form"), true));

        await rig.Build().HandleEventAsync(Submitted());

        rig.QueuedRows().ShouldHaveSingleItem().TypeId.ShouldBeNull();
    }
}
