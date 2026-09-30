using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Documents;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.PackageDetails;

/// <summary>
/// Covers <see cref="IPackageDetailsAppService"/>, which reported 7.1% before this file -- and that 7.1%
/// was the extracted <c>ComputeLinkSetDiff</c> helper, already pinned by
/// <c>PackageDetailsLinkSetDiffUnitTests</c>. Nothing here re-tests the helper; it covers its caller.
///
/// <para>THE RULE THIS SERVICE OWNS: at most one ACTIVE package per <c>AppointmentTypeId</c>
/// (<c>EnsureNoActiveDuplicateAsync</c>). Every guard Fact below is paired with a fixture that CONTAINS
/// the thing the guard must ignore -- an inactive package, an active package of another type -- because a
/// refusal asserted against an empty table passes with half the predicate deleted.</para>
///
/// <para>TWO THINGS THIS FILE DELIBERATELY DOES NOT PIN. (1) CreateAsync applies the rule even when the
/// new package is INACTIVE, while UpdateAsync applies it to active packages only. Whether that asymmetry
/// is intended is an open product question (#1002), so no Fact takes a side on it: every fixture that
/// needs an inactive package next to an active one creates the inactive one FIRST, while its type has no
/// active package, and so never reaches the disputed path. (2) The <c>excludingId</c> clause in
/// <c>EnsureNoActiveDuplicateAsync</c> cannot change a result: UpdateAsync only calls it when the package
/// is becoming active or changing type, so the persisted row is inactive or on its old type and can never
/// match its own query. No Fact claims to test it.</para>
///
/// <para>LINK ROWS ARE HARD-DELETED. <c>DocumentPackage : Entity</c> has no <c>ISoftDelete</c>, so
/// DeleteAsync removes every link row outright and soft-deletes only the package itself.</para>
///
/// <para>THE RIG ACCUMULATES: one SQLite connection for the whole collection, no rollback. Every Fact
/// uses its own random <c>AppointmentTypeId</c> -- it is NOT a foreign key, so an invented id isolates the
/// per-type rule completely -- and filters lists by a unique token. <c>DocumentPackage.DocumentId</c> IS a
/// required foreign key, so link Facts seed real <c>Document</c> rows. A Document is metadata only, so that
/// needs no blob store. No authorization Facts: <c>AddAlwaysAllowAuthorization()</c> makes every
/// <c>[Authorize]</c> here inert.</para>
/// </summary>
public abstract class PackageDetailsAppServiceTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IPackageDetailsAppService _packages;
    private readonly IPackageDetailRepository _packageRepository;
    private readonly IRepository<DocumentPackage> _linkRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    protected PackageDetailsAppServiceTests()
    {
        _packages = GetRequiredService<IPackageDetailsAppService>();
        _packageRepository = GetRequiredService<IPackageDetailRepository>();
        _linkRepository = GetRequiredService<IRepository<DocumentPackage>>();
        _documentRepository = GetRequiredService<IDocumentRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _dataFilter = GetRequiredService<IDataFilter>();
    }

    private static string Token(string label) => $"TEST-pkg-{label}-{Guid.NewGuid():N}"[..40];

    private async Task<PackageDetailDto> CreateAsync(string name, Guid appointmentTypeId, bool isActive = true)
    {
        return await _packages.CreateAsync(new PackageDetailCreateDto
        {
            PackageName = name,
            AppointmentTypeId = appointmentTypeId,
            IsActive = isActive,
        });
    }

    /// <summary>
    /// Seeds a real <c>Document</c>. The name prefix is <c>TEST-pkgdoc-</c>, NOT <c>TEST-doc-</c>, so these
    /// rows can never fall inside the name filters <c>EfCoreDocumentsAppServiceTests</c> counts against.
    /// </summary>
    private Task<Guid> SeedDocumentAsync(string label) =>
        WithUnitOfWorkAsync(async () =>
        {
            var id = Guid.NewGuid();
            await _documentRepository.InsertAsync(
                new Document(
                    id: id,
                    tenantId: _currentTenant.Id,
                    name: $"TEST-pkgdoc-{label}-{id:N}"[..40],
                    blobName: $"TEST-blob-{id:N}.pdf",
                    contentType: "application/pdf"),
                autoSave: true);
            return id;
        });

    private Task SeedLinkAsync(Guid packageId, Guid documentId, bool isActive = true) =>
        WithUnitOfWorkAsync(async () =>
        {
            await _linkRepository.InsertAsync(new DocumentPackage(packageId, documentId, isActive), autoSave: true);
        });

    private Task<List<Guid>> LinkedDocumentIdsAsync(Guid packageId) =>
        WithUnitOfWorkAsync(async () =>
            (await _linkRepository.GetListAsync(x => x.PackageDetailId == packageId))
                .Select(x => x.DocumentId)
                .ToList());

    private async Task<List<PackageDetailDto>> ListAsync(string filterText, string? sorting)
    {
        var page = await _packages.GetListAsync(new GetPackageDetailsInput
        {
            FilterText = filterText,
            Sorting = sorting,
            MaxResultCount = 50,
        });
        return page.Items.ToList();
    }

    // ------------------------------------------------------------------------
    // Create, and the one-active-package-per-type rule.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_PersistsThePackage()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var name = Token("create");
            var type = Guid.NewGuid();

            var created = await CreateAsync(name, type);

            var fetched = await _packages.GetAsync(created.Id);
            fetched.PackageName.ShouldBe(name);
            fetched.AppointmentTypeId.ShouldBe(type);
            fetched.IsActive.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task CreateAsync_ASecondActivePackageForTheSameType_IsRefused()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var type = Guid.NewGuid();
            await CreateAsync(Token("dup-first"), type);
            var second = new PackageDetailCreateDto
            {
                PackageName = Token("dup-second"),
                AppointmentTypeId = type,
                IsActive = true,
            };

            var ex = await Should.ThrowAsync<BusinessException>(
                async () => await _packages.CreateAsync(second));

            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.OneActivePackageDetailPerAppointmentType);
        }
    }

    [Fact]
    public async Task CreateAsync_AnInactivePackageOfTheSameTypeDoesNotBlockAnActiveOne()
    {
        // LOAD-BEARING FIXTURE: the inactive package is the thing the guard must IGNORE. Without it, a
        // guard with `x.IsActive` deleted would pass this Fact too. It is created first, while the type
        // has no active package, so the fixture never touches the disputed create path (#1002).
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var type = Guid.NewGuid();
            await CreateAsync(Token("inactive-first"), type, isActive: false);

            var active = await CreateAsync(Token("active-after"), type);

            active.IsActive.ShouldBeTrue();
            active.AppointmentTypeId.ShouldBe(type);
        }
    }

    [Fact]
    public async Task CreateAsync_AnActivePackageOfAnotherTypeDoesNotBlock()
    {
        // LOAD-BEARING FIXTURE: an active package of a DIFFERENT type. With the type predicate deleted,
        // any active package anywhere in the tenant would count as a duplicate.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await CreateAsync(Token("other-type"), Guid.NewGuid());
            var type = Guid.NewGuid();

            var created = await CreateAsync(Token("this-type"), type);

            created.AppointmentTypeId.ShouldBe(type);
        }
    }

    // ------------------------------------------------------------------------
    // Update: the rule re-applies only when a package becomes active or changes type.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_ActivatingASecondPackageForAType_IsRefused()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var type = Guid.NewGuid();
            // ORDER IS LOAD-BEARING: the dormant package is created BEFORE the incumbent, so the create
            // path never applies the rule at the limit (#1002). Reversed, the fixture itself is refused.
            var dormant = await CreateAsync(Token("dormant"), type, isActive: false);
            await CreateAsync(Token("incumbent"), type);
            var activate = new PackageDetailUpdateDto
            {
                PackageName = dormant.PackageName,
                AppointmentTypeId = type,
                IsActive = true,
            };

            var ex = await Should.ThrowAsync<BusinessException>(
                async () => await _packages.UpdateAsync(dormant.Id, activate));

            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.OneActivePackageDetailPerAppointmentType);
        }
    }

    [Fact]
    public async Task UpdateAsync_MovingAnActivePackageOntoAnOccupiedType_IsRefused()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var occupied = Guid.NewGuid();
            await CreateAsync(Token("occupant"), occupied);
            var mover = await CreateAsync(Token("mover"), Guid.NewGuid());
            var move = new PackageDetailUpdateDto
            {
                PackageName = mover.PackageName,
                AppointmentTypeId = occupied,
                IsActive = true,
            };

            var ex = await Should.ThrowAsync<BusinessException>(
                async () => await _packages.UpdateAsync(mover.Id, move));

            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.OneActivePackageDetailPerAppointmentType);
        }
    }

    [Fact]
    public async Task UpdateAsync_ARenameIsPersisted()
    {
        // A persistence Fact, not a guard Fact: a rename neither activates nor moves the package, so the
        // rule is not re-applied at all. It does NOT test the dead `excludingId` clause -- see the class
        // docstring.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var type = Guid.NewGuid();
            var package = await CreateAsync(Token("rename"), type);
            var newName = Token("renamed");

            var updated = await _packages.UpdateAsync(package.Id, new PackageDetailUpdateDto
            {
                PackageName = newName,
                AppointmentTypeId = type,
                IsActive = true,
            });

            updated.PackageName.ShouldBe(newName);
            (await _packages.GetAsync(package.Id)).PackageName.ShouldBe(newName);
        }
    }

    // ------------------------------------------------------------------------
    // Documents: read, link, unlink.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetWithDocumentsAsync_ForAPackageWithNoLinks_ReturnsAnEmptyList()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var package = await CreateAsync(Token("nolinks"), Guid.NewGuid());

            var result = await _packages.GetWithDocumentsAsync(package.Id);

            result.Package.Id.ShouldBe(package.Id);
            result.LinkedDocuments.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task GetWithDocumentsAsync_ListsOnlyDocumentsBehindActiveLinks()
    {
        // LOAD-BEARING FIXTURE: the INACTIVE link. Without it, a query that ignored IsActive on the link
        // would return the same single document and pass.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var package = await CreateAsync(Token("links"), Guid.NewGuid());
            var shown = await SeedDocumentAsync("shown");
            var hidden = await SeedDocumentAsync("hidden");
            await SeedLinkAsync(package.Id, shown);
            await SeedLinkAsync(package.Id, hidden, isActive: false);

            var result = await _packages.GetWithDocumentsAsync(package.Id);

            // Only a document behind an ACTIVE link belongs on the package.
            result.LinkedDocuments.Select(d => d.Id).ToList().ShouldBe(new List<Guid> { shown });
        }
    }

    [Fact]
    public async Task LinkDocumentsAsync_AddsTheNewDocumentsAndRemovesTheDroppedOnes()
    {
        // LOAD-BEARING FIXTURE: an existing link the caller DROPS. Adding alone would pass with the
        // removal half of the diff never applied.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var package = await CreateAsync(Token("relink"), Guid.NewGuid());
            var kept = await SeedDocumentAsync("kept");
            var dropped = await SeedDocumentAsync("dropped");
            var added = await SeedDocumentAsync("added");
            await SeedLinkAsync(package.Id, kept);
            await SeedLinkAsync(package.Id, dropped);
            var desired = new List<Guid> { kept, added };

            var result = await _packages.LinkDocumentsAsync(package.Id, desired);

            (await LinkedDocumentIdsAsync(package.Id)).ShouldBe(desired, ignoreOrder: true);
            result.LinkedDocuments.Select(d => d.Id).ShouldBe(desired, ignoreOrder: true);
        }
    }

    [Fact]
    public async Task LinkDocumentsAsync_ForAnUnknownPackage_IsRefusedBeforeAnyLinkIsWritten()
    {
        // THE EXCEPTION TYPE IS THE POINT. The service touches the package first so it fails with
        // EntityNotFoundException BEFORE writing. Were that touch removed, the insert would still fail --
        // but at save time, on the PackageDetailId foreign key, as a DbUpdateException. Asserting the
        // specific type is what tells "refused before writing" apart from "refused by the database".
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var unknown = Guid.NewGuid();
            var document = await SeedDocumentAsync("orphan");
            var ids = new List<Guid> { document };

            await Should.ThrowAsync<EntityNotFoundException>(
                async () => await _packages.LinkDocumentsAsync(unknown, ids));

            (await LinkedDocumentIdsAsync(unknown)).ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task UnlinkDocumentAsync_RemovesThatLinkAndNoOther()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var package = await CreateAsync(Token("unlink"), Guid.NewGuid());
            var removed = await SeedDocumentAsync("removed");
            var remaining = await SeedDocumentAsync("remaining");
            await SeedLinkAsync(package.Id, removed);
            await SeedLinkAsync(package.Id, remaining);

            await _packages.UnlinkDocumentAsync(package.Id, removed);

            (await LinkedDocumentIdsAsync(package.Id)).ShouldBe(new List<Guid> { remaining });
        }
    }

    [Fact]
    public async Task UnlinkDocumentAsync_ForALinkThatDoesNotExist_IsANoOp()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var package = await CreateAsync(Token("unlink-none"), Guid.NewGuid());
            var linked = await SeedDocumentAsync("linked");
            await SeedLinkAsync(package.Id, linked);

            // Called directly, not through Should.NotThrowAsync: an escaping exception fails the Fact
            // just as loudly, and the assertion below is the part that discriminates.
            await _packages.UnlinkDocumentAsync(package.Id, Guid.NewGuid());

            (await LinkedDocumentIdsAsync(package.Id)).ShouldBe(new List<Guid> { linked });
        }
    }

    // ------------------------------------------------------------------------
    // Delete -- the OLD-bug-fix the class docstring names.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_RemovesEveryLinkRowAndSoftDeletesThePackage()
    {
        // THREE LINKS, not one. OLD removed only the FIRST link row and orphaned the rest; against a
        // single link, "every" and "the first" are the same thing and this Fact could not tell them apart.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var package = await CreateAsync(Token("delete"), Guid.NewGuid());
            foreach (var label in new[] { "one", "two", "three" })
            {
                await SeedLinkAsync(package.Id, await SeedDocumentAsync(label));
            }

            await _packages.DeleteAsync(package.Id);

            (await LinkedDocumentIdsAsync(package.Id)).ShouldBeEmpty(
                "Every link row must go. They are HARD-deleted: DocumentPackage has no ISoftDelete.");
            await Should.ThrowAsync<EntityNotFoundException>(
                async () => await _packages.GetAsync(package.Id));
            await WithUnitOfWorkAsync(async () =>
            {
                using (_dataFilter.Disable<ISoftDelete>())
                {
                    (await _packageRepository.GetAsync(package.Id)).IsDeleted.ShouldBeTrue(
                        "The package itself is SOFT-deleted, not removed.");
                }
            });
        }
    }

    // ------------------------------------------------------------------------
    // GetListAsync -- filters, both directions, scoped by token or by a per-Fact type.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetListAsync_FiltersByTextInBothDirections()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var marker = Token("text");
            await CreateAsync(marker, Guid.NewGuid());
            await CreateAsync(Token("text-other"), Guid.NewGuid());

            var hit = await _packages.GetListAsync(new GetPackageDetailsInput { FilterText = marker, MaxResultCount = 50 });
            var miss = await _packages.GetListAsync(new GetPackageDetailsInput { FilterText = Token("text-none"), MaxResultCount = 50 });

            hit.Items.Select(x => x.PackageName).ToList().ShouldBe(new List<string> { marker });
            hit.TotalCount.ShouldBe(1L, "The count is taken under the same filter as the page.");
            miss.Items.ShouldBeEmpty("A filter nothing matches must return nothing.");
        }
    }

    [Fact]
    public async Task GetListAsync_FiltersByAppointmentTypeInBothDirections()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var type = Guid.NewGuid();
            var mine = await CreateAsync(Token("bytype"), type);
            await CreateAsync(Token("bytype-other"), Guid.NewGuid());

            var hit = await _packages.GetListAsync(new GetPackageDetailsInput { AppointmentTypeId = type, MaxResultCount = 50 });
            var miss = await _packages.GetListAsync(new GetPackageDetailsInput { AppointmentTypeId = Guid.NewGuid(), MaxResultCount = 50 });

            hit.Items.Select(x => x.Id).ToList().ShouldBe(new List<Guid> { mine.Id });
            miss.Items.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task GetListAsync_FiltersByActiveStateInBothDirections()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var type = Guid.NewGuid();
            // Inactive first, for the same #1002 reason as the Update Facts.
            var dormant = await CreateAsync(Token("idle"), type, isActive: false);
            var live = await CreateAsync(Token("live"), type);

            var active = await _packages.GetListAsync(new GetPackageDetailsInput { AppointmentTypeId = type, IsActive = true, MaxResultCount = 50 });
            var inactive = await _packages.GetListAsync(new GetPackageDetailsInput { AppointmentTypeId = type, IsActive = false, MaxResultCount = 50 });

            active.Items.Select(x => x.Id).ToList().ShouldBe(new List<Guid> { live.Id });
            inactive.Items.Select(x => x.Id).ToList().ShouldBe(new List<Guid> { dormant.Id });
        }
    }

    // ------------------------------------------------------------------------
    // GetListAsync -- the five-arm sort switch.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetListAsync_SortsByNameInBothDirections()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var prefix = Token("sort-name");
            // Inserted b, c, a -- so neither insertion order nor its reverse equals name order.
            foreach (var suffix in new[] { "b", "c", "a" })
            {
                await CreateAsync($"{prefix}-{suffix}", Guid.NewGuid());
            }
            var ascending = new List<string> { $"{prefix}-a", $"{prefix}-b", $"{prefix}-c" };

            (await ListAsync(prefix, "packagename desc")).Select(x => x.PackageName).ToList()
                .ShouldBe(Enumerable.Reverse(ascending).ToList());
            // The switch lower-cases its input, so casing must not matter.
            (await ListAsync(prefix, "PackageName ASC")).Select(x => x.PackageName).ToList()
                .ShouldBe(ascending);
            (await ListAsync(prefix, "packagename")).Select(x => x.PackageName).ToList()
                .ShouldBe(ascending);
        }
    }

    [Fact]
    public async Task GetListAsync_SortsByCreationTimeInBothDirections()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var prefix = Token("sort-time");
            foreach (var suffix in new[] { "b", "c", "a" })
            {
                await CreateAsync($"{prefix}-{suffix}", Guid.NewGuid());
            }

            var rows = await ListAsync(prefix, "packagename");
            // PRECONDITION, stated so a vacuous pass is impossible: if two rows shared a CreationTime,
            // every order of them would satisfy the assertions below and this Fact would prove nothing.
            rows.Select(x => x.CreationTime).Distinct().Count().ShouldBe(
                3, "FIXTURE PRECONDITION FAILED: creation times tie, so the sort is unobservable.");
            // Expected orders come from the CreationTime READ BACK, never from insertion order.
            var oldestFirst = rows.OrderBy(x => x.CreationTime).Select(x => x.Id).ToList();

            (await ListAsync(prefix, "creationtime asc")).Select(x => x.Id).ToList().ShouldBe(oldestFirst);
            (await ListAsync(prefix, "creationtime")).Select(x => x.Id).ToList().ShouldBe(oldestFirst);
            (await ListAsync(prefix, "creationtime desc")).Select(x => x.Id).ToList()
                .ShouldBe(Enumerable.Reverse(oldestFirst).ToList());
        }
    }

    [Fact]
    public async Task GetListAsync_WithAnUnrecognisedOrEmptySort_OrdersByNameAscending()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var prefix = Token("sort-default");
            foreach (var suffix in new[] { "b", "c", "a" })
            {
                await CreateAsync($"{prefix}-{suffix}", Guid.NewGuid());
            }
            var ascending = new List<string> { $"{prefix}-a", $"{prefix}-b", $"{prefix}-c" };

            // An unrecognised sort falls through to name ascending.
            (await ListAsync(prefix, "no-such-column")).Select(x => x.PackageName).ToList()
                .ShouldBe(ascending);
            // An empty sort defaults to PackageName.
            (await ListAsync(prefix, "")).Select(x => x.PackageName).ToList()
                .ShouldBe(ascending);
            // So does a missing one.
            (await ListAsync(prefix, null)).Select(x => x.PackageName).ToList()
                .ShouldBe(ascending);
        }
    }
}
