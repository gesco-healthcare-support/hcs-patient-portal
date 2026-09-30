using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.CustomFields;

/// <summary>
/// Covers <see cref="ICustomFieldsAppService"/>, which reported 1.9% before this file -- the extracted
/// <c>IsAtOrOverCap</c> and <c>ComputeNextDisplayOrder</c> helpers, already pinned by
/// <c>CustomFieldsAppServiceUnitTests</c> (13 cases). In particular the helper ALREADY pins the
/// docstring's `&gt;= 10, not == 10` correction; what nothing pinned is the PER-TYPE scoping of the query
/// that feeds it, which is the other half of the same claim and is covered here.
///
/// <para>THE SERVICE'S RULES: at most 10 ACTIVE fields per <c>AppointmentTypeId</c>; no two fields
/// sharing a label AND a field type; <c>DisplayOrder</c> = the catalog maximum + 1 on create. Each guard
/// Fact is paired with a fixture that CONTAINS what the guard must ignore -- another type holding 10,
/// inactive rows, a same-label field of another type -- because a refusal asserted against an empty
/// table passes with half the predicate deleted.</para>
///
/// <para>THE AT-CAP AND OVER-CAP STATES ARE SEEDED THROUGH THE REPOSITORY, bypassing the service's own
/// cap. That is the only way to reach them, and the over-cap state (11 active) is load-bearing for one
/// Fact: see <c>UpdateAsync_AnEditThatNeitherActivatesNorMovesAField_IsNotReChecked</c>.</para>
///
/// <para>TWO THINGS THIS FILE DELIBERATELY DOES NOT PIN. (1) CreateAsync applies the cap even to an
/// INACTIVE new field, while UpdateAsync applies it to active fields only. Whether that asymmetry is
/// intended is an open product question (#1002), so no Fact takes a side: fixtures needing an inactive
/// field in a full type seed it through the repository, not the service. (2) The <c>excludingId</c> in
/// the cap check cannot change a result -- UpdateAsync only calls it when the field is becoming active or
/// changing type, so the persisted row is never counted against itself. The DUPLICATE-LABEL check's
/// <c>excludingId</c> is different: it runs on EVERY update and is live, and is pinned below.</para>
///
/// <para>THE RIG ACCUMULATES: one SQLite connection for the whole collection, no rollback. Every Fact uses
/// its own random <c>AppointmentTypeId</c> -- NOT a foreign key -- and unique token labels. The DisplayOrder
/// maximum is catalog-wide, so its Fact asserts RELATIVE to a maximum read inside the same Fact. Seeded rows
/// use DisplayOrder 1 so fixtures never inflate that maximum for anyone else. <c>CustomFieldType</c> runs
/// 12-18 and <c>default</c> (0) is not a member, so every field sets its type explicitly. No
/// authorization Facts: <c>AddAlwaysAllowAuthorization()</c> makes every <c>[Authorize]</c> inert.</para>
/// </summary>
public abstract class CustomFieldsAppServiceTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const int Cap = CustomFieldConsts.MaxActiveCountPerAppointmentType;

    private readonly ICustomFieldsAppService _fields;
    private readonly ICustomFieldRepository _fieldRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    protected CustomFieldsAppServiceTests()
    {
        _fields = GetRequiredService<ICustomFieldsAppService>();
        _fieldRepository = GetRequiredService<ICustomFieldRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _dataFilter = GetRequiredService<IDataFilter>();
    }

    private static string Token(string label) => $"TEST-cf-{label}-{Guid.NewGuid():N}"[..40];

    private async Task<CustomFieldDto> CreateAsync(
        string label,
        Guid appointmentTypeId,
        CustomFieldType fieldType = CustomFieldType.Alphanumeric,
        bool isActive = true)
    {
        return await _fields.CreateAsync(new CustomFieldCreateDto
        {
            FieldLabel = label,
            FieldType = fieldType,
            AppointmentTypeId = appointmentTypeId,
            IsActive = isActive,
        });
    }

    /// <summary>
    /// Seeds rows straight through the repository, bypassing the service's cap -- the only way to reach
    /// the at-cap and over-cap states. DisplayOrder 1 unless stated, so the catalog maximum is not raised.
    /// </summary>
    private Task<List<CustomField>> SeedAsync(Guid appointmentTypeId, int count, bool isActive = true) =>
        WithUnitOfWorkAsync(async () =>
        {
            var rows = new List<CustomField>();
            for (var i = 0; i < count; i++)
            {
                rows.Add(await _fieldRepository.InsertAsync(
                    new CustomField(
                        id: Guid.NewGuid(),
                        tenantId: _currentTenant.Id,
                        fieldLabel: Token($"seed{i}"),
                        displayOrder: 1,
                        fieldType: CustomFieldType.Alphanumeric,
                        appointmentTypeId: appointmentTypeId,
                        isActive: isActive),
                    autoSave: true));
            }
            return rows;
        });

    private Task<CustomField> SeedOneAsync(Guid appointmentTypeId, string label, int displayOrder, bool isActive = true) =>
        WithUnitOfWorkAsync(async () =>
            await _fieldRepository.InsertAsync(
                new CustomField(
                    id: Guid.NewGuid(),
                    tenantId: _currentTenant.Id,
                    fieldLabel: label,
                    displayOrder: displayOrder,
                    fieldType: CustomFieldType.Alphanumeric,
                    appointmentTypeId: appointmentTypeId,
                    isActive: isActive),
                autoSave: true));

    private static CustomFieldUpdateDto UpdateOf(CustomField row) => new()
    {
        FieldLabel = row.FieldLabel,
        DisplayOrder = row.DisplayOrder,
        FieldType = row.FieldType,
        AppointmentTypeId = row.AppointmentTypeId,
        IsActive = row.IsActive,
    };

    private static CustomFieldUpdateDto UpdateOf(CustomFieldDto dto) => new()
    {
        FieldLabel = dto.FieldLabel,
        DisplayOrder = dto.DisplayOrder,
        FieldType = dto.FieldType,
        AppointmentTypeId = dto.AppointmentTypeId,
        IsActive = dto.IsActive,
    };

    private async Task<List<CustomFieldDto>> ListAsync(string filterText, string? sorting)
    {
        var page = await _fields.GetListAsync(new GetCustomFieldsInput
        {
            FilterText = filterText,
            Sorting = sorting,
            MaxResultCount = 50,
        });
        return page.Items.ToList();
    }

    // ------------------------------------------------------------------------
    // Create.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_PersistsEveryField()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var label = Token("create");
            var type = Guid.NewGuid();

            var created = await _fields.CreateAsync(new CustomFieldCreateDto
            {
                FieldLabel = label,
                FieldType = CustomFieldType.Numeric,
                FieldLength = 12,
                DefaultValue = "TEST-default",
                IsMandatory = true,
                AppointmentTypeId = type,
                IsActive = true,
            });

            var fetched = await _fields.GetAsync(created.Id);
            fetched.FieldLabel.ShouldBe(label);
            fetched.FieldType.ShouldBe(CustomFieldType.Numeric);
            fetched.FieldLength.ShouldBe(12);
            fetched.DefaultValue.ShouldBe("TEST-default");
            fetched.IsMandatory.ShouldBeTrue();
            fetched.AppointmentTypeId.ShouldBe(type);
            fetched.IsActive.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task CreateAsync_AssignsTheCatalogMaximumDisplayOrderPlusOne()
    {
        // RELATIVE, never absolute: the maximum is catalog-wide in a database every Fact writes to, so
        // it is read inside this Fact, immediately before the create it predicts.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await CreateAsync(Token("order-seed"), Guid.NewGuid());
            var max = await WithUnitOfWorkAsync(async () =>
                (await _fieldRepository.GetListAsync()).Max(x => x.DisplayOrder));

            var created = await CreateAsync(Token("order-next"), Guid.NewGuid());

            created.DisplayOrder.ShouldBe(max + 1);
        }
    }

    // ------------------------------------------------------------------------
    // The per-type active cap, on Create.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_TheEleventhActiveFieldForAType_IsRefused()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var type = Guid.NewGuid();
            await SeedAsync(type, Cap);
            var eleventh = new CustomFieldCreateDto
            {
                FieldLabel = Token("eleventh"),
                FieldType = CustomFieldType.Alphanumeric,
                AppointmentTypeId = type,
                IsActive = true,
            };

            var ex = await Should.ThrowAsync<BusinessException>(
                async () => await _fields.CreateAsync(eleventh));

            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.CustomFieldMax10ActivePerAppointmentType);
        }
    }

    [Fact]
    public async Task CreateAsync_AnotherTypeHoldingTenActiveFields_DoesNotBlock()
    {
        // LOAD-BEARING FIXTURE, and the unpinned half of the docstring's per-type correction: a DIFFERENT
        // type already at the cap. With the type predicate deleted the count is tenant-wide and this
        // create is refused.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await SeedAsync(Guid.NewGuid(), Cap);
            var type = Guid.NewGuid();

            var created = await CreateAsync(Token("other-full"), type);

            created.AppointmentTypeId.ShouldBe(type);
        }
    }

    [Fact]
    public async Task CreateAsync_InactiveFieldsDoNotCountTowardTheCap()
    {
        // LOAD-BEARING FIXTURE: ten INACTIVE fields in this very type. With `x.IsActive` deleted from the
        // count they would fill the cap and this create would be refused.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var type = Guid.NewGuid();
            await SeedAsync(type, Cap, isActive: false);

            var created = await CreateAsync(Token("despite-inactive"), type);

            created.IsActive.ShouldBeTrue();
        }
    }

    // ------------------------------------------------------------------------
    // The per-type active cap, on Update -- re-applied only when a field activates or moves.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_ActivatingAFieldInAFullType_IsRefused()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var type = Guid.NewGuid();
            await SeedAsync(type, Cap);
            // Seeded, not created: creating an inactive field in a full type through the service is the
            // disputed path (#1002).
            var dormant = (await SeedAsync(type, 1, isActive: false)).Single();
            var activate = UpdateOf(dormant);
            activate.IsActive = true;

            var ex = await Should.ThrowAsync<BusinessException>(
                async () => await _fields.UpdateAsync(dormant.Id, activate));

            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.CustomFieldMax10ActivePerAppointmentType);
        }
    }

    [Fact]
    public async Task UpdateAsync_MovingAnActiveFieldIntoAFullType_IsRefused()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var full = Guid.NewGuid();
            await SeedAsync(full, Cap);
            var mover = await CreateAsync(Token("mover"), Guid.NewGuid());
            var move = UpdateOf(mover);
            move.AppointmentTypeId = full;

            var ex = await Should.ThrowAsync<BusinessException>(
                async () => await _fields.UpdateAsync(mover.Id, move));

            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.CustomFieldMax10ActivePerAppointmentType);
        }
    }

    [Fact]
    public async Task UpdateAsync_AnEditThatNeitherActivatesNorMovesAField_IsNotReChecked()
    {
        // WHY ELEVEN, NOT TEN -- the fixture is the whole Fact. The cap check excludes the row being
        // edited. With exactly 10 active fields in the type, a service that ALWAYS re-checked would count
        // the other 9, find them under the cap, and pass -- so a 10-row fixture cannot observe whether the
        // check was skipped. With 11 (an over-cap state only the repository can create), an always-check
        // counts 10 others and refuses. Only then does "not re-checked" become visible.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var type = Guid.NewGuid();
            var target = (await SeedAsync(type, Cap + 1)).First();
            var relabel = UpdateOf(target);
            relabel.FieldLabel = Token("relabelled");

            var updated = await _fields.UpdateAsync(target.Id, relabel);

            updated.FieldLabel.ShouldBe(relabel.FieldLabel);
        }
    }

    // ------------------------------------------------------------------------
    // The duplicate label + field type guard.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ADuplicateLabelAndFieldType_IsRefused()
    {
        // Same appointment type on purpose: whether the guard SHOULD span types is not something this
        // Fact has any business deciding, and a same-type duplicate is refused under either reading.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var type = Guid.NewGuid();
            var label = Token("dup");
            await CreateAsync(label, type);
            var duplicate = new CustomFieldCreateDto
            {
                FieldLabel = label,
                FieldType = CustomFieldType.Alphanumeric,
                AppointmentTypeId = type,
                IsActive = true,
            };

            var ex = await Should.ThrowAsync<BusinessException>(
                async () => await _fields.CreateAsync(duplicate));

            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.CustomFieldDuplicateLabelAndType);
        }
    }

    [Fact]
    public async Task CreateAsync_TheSameLabelWithADifferentFieldType_IsAllowed()
    {
        // LOAD-BEARING FIXTURE: the same label already exists with ANOTHER field type. With the
        // FieldType predicate deleted, the guard keys on label alone and refuses this.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var type = Guid.NewGuid();
            var label = Token("same-label");
            await CreateAsync(label, type, CustomFieldType.Alphanumeric);

            var created = await CreateAsync(label, type, CustomFieldType.Numeric);

            created.FieldType.ShouldBe(CustomFieldType.Numeric);
        }
    }

    [Fact]
    public async Task UpdateAsync_WithoutChangingLabelOrFieldType_IsNotRefusedAsItsOwnDuplicate()
    {
        // THE LIVE excludingId. The duplicate check runs on EVERY update, and the row being edited
        // already carries this label and type -- without excluding itself it would refuse every edit.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var field = await CreateAsync(Token("self"), Guid.NewGuid());
            var edit = UpdateOf(field);
            edit.IsMandatory = true;

            var updated = await _fields.UpdateAsync(field.Id, edit);

            updated.IsMandatory.ShouldBeTrue();
            (await _fields.GetAsync(field.Id)).IsMandatory.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task UpdateAsync_OntoAnotherFieldsLabelAndFieldType_IsRefused()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var type = Guid.NewGuid();
            var taken = await CreateAsync(Token("taken"), type);
            var other = await CreateAsync(Token("other"), type);
            var collide = UpdateOf(other);
            collide.FieldLabel = taken.FieldLabel;

            var ex = await Should.ThrowAsync<BusinessException>(
                async () => await _fields.UpdateAsync(other.Id, collide));

            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.CustomFieldDuplicateLabelAndType);
        }
    }

    // ------------------------------------------------------------------------
    // GetActiveForAppointmentTypeAsync -- what the booking form reads.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetActiveForAppointmentTypeAsync_ReturnsOnlyThatTypesActiveFieldsInDisplayOrder()
    {
        // LOAD-BEARING FIXTURE: an INACTIVE field of this type and an ACTIVE field of another type, both
        // of which must be left out; and active fields seeded OUT of display order (30 before 10), so an
        // unordered or reversed query cannot pass by accident.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var type = Guid.NewGuid();
            var thirty = await SeedOneAsync(type, Token("thirty"), displayOrder: 30);
            var ten = await SeedOneAsync(type, Token("ten"), displayOrder: 10);
            await SeedOneAsync(type, Token("inactive"), displayOrder: 20, isActive: false);
            await SeedOneAsync(Guid.NewGuid(), Token("other-type"), displayOrder: 15);

            var result = await _fields.GetActiveForAppointmentTypeAsync(type);

            result.Select(x => x.Id).ToList().ShouldBe(new List<Guid> { ten.Id, thirty.Id });
        }
    }

    // ------------------------------------------------------------------------
    // GetListAsync -- filters, both directions.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetListAsync_FiltersByTextInBothDirections()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var marker = Token("text");
            await CreateAsync(marker, Guid.NewGuid());
            await CreateAsync(Token("text-other"), Guid.NewGuid());

            var hit = await _fields.GetListAsync(new GetCustomFieldsInput { FilterText = marker, MaxResultCount = 50 });
            var miss = await _fields.GetListAsync(new GetCustomFieldsInput { FilterText = Token("text-none"), MaxResultCount = 50 });

            hit.Items.Select(x => x.FieldLabel).ToList().ShouldBe(new List<string> { marker });
            hit.TotalCount.ShouldBe(1L, "The count is taken under the same filter as the page.");
            miss.Items.ShouldBeEmpty("A filter nothing matches must return nothing.");
        }
    }

    [Fact]
    public async Task GetListAsync_FiltersByAppointmentTypeAndActiveStateInBothDirections()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var type = Guid.NewGuid();
            var live = await CreateAsync(Token("live"), type);
            var idle = await CreateAsync(Token("idle"), type, isActive: false);

            var byType = await _fields.GetListAsync(new GetCustomFieldsInput { AppointmentTypeId = type, MaxResultCount = 50 });
            var active = await _fields.GetListAsync(new GetCustomFieldsInput { AppointmentTypeId = type, IsActive = true, MaxResultCount = 50 });
            var inactive = await _fields.GetListAsync(new GetCustomFieldsInput { AppointmentTypeId = type, IsActive = false, MaxResultCount = 50 });
            var elsewhere = await _fields.GetListAsync(new GetCustomFieldsInput { AppointmentTypeId = Guid.NewGuid(), MaxResultCount = 50 });

            byType.Items.Select(x => x.Id).ShouldBe(new List<Guid> { live.Id, idle.Id }, ignoreOrder: true);
            active.Items.Select(x => x.Id).ToList().ShouldBe(new List<Guid> { live.Id });
            inactive.Items.Select(x => x.Id).ToList().ShouldBe(new List<Guid> { idle.Id });
            elsewhere.Items.ShouldBeEmpty();
        }
    }

    // ------------------------------------------------------------------------
    // GetListAsync -- the seven-arm sort switch.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetListAsync_SortsByDisplayOrderAndByLabelInBothDirections()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var prefix = Token("sort");
            // Label order (a, b, c) and display order (b, c, a) deliberately DISAGREE, so each sort is
            // distinguishable from the other and from insertion order.
            await SeedOneAsync(Guid.NewGuid(), $"{prefix}-a", displayOrder: 3);
            await SeedOneAsync(Guid.NewGuid(), $"{prefix}-b", displayOrder: 1);
            await SeedOneAsync(Guid.NewGuid(), $"{prefix}-c", displayOrder: 2);
            var byLabel = new List<string> { $"{prefix}-a", $"{prefix}-b", $"{prefix}-c" };
            var byOrder = new List<string> { $"{prefix}-b", $"{prefix}-c", $"{prefix}-a" };

            (await ListAsync(prefix, "fieldlabel")).Select(x => x.FieldLabel).ToList().ShouldBe(byLabel);
            // The switch lower-cases its input, so casing must not matter.
            (await ListAsync(prefix, "FieldLabel ASC")).Select(x => x.FieldLabel).ToList()
                .ShouldBe(byLabel);
            (await ListAsync(prefix, "fieldlabel desc")).Select(x => x.FieldLabel).ToList()
                .ShouldBe(Enumerable.Reverse(byLabel).ToList());
            (await ListAsync(prefix, "displayorder")).Select(x => x.FieldLabel).ToList().ShouldBe(byOrder);
            (await ListAsync(prefix, "displayorder asc")).Select(x => x.FieldLabel).ToList().ShouldBe(byOrder);
            (await ListAsync(prefix, "displayorder desc")).Select(x => x.FieldLabel).ToList()
                .ShouldBe(Enumerable.Reverse(byOrder).ToList());
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

            var rows = await ListAsync(prefix, "fieldlabel");
            // PRECONDITION, stated so a vacuous pass is impossible: tied creation times would let every
            // order satisfy the assertions below.
            rows.Select(x => x.CreationTime).Distinct().Count().ShouldBe(
                3, "FIXTURE PRECONDITION FAILED: creation times tie, so the sort is unobservable.");
            var oldestFirst = rows.OrderBy(x => x.CreationTime).Select(x => x.Id).ToList();

            (await ListAsync(prefix, "creationtime asc")).Select(x => x.Id).ToList().ShouldBe(oldestFirst);
            (await ListAsync(prefix, "creationtime")).Select(x => x.Id).ToList().ShouldBe(oldestFirst);
            (await ListAsync(prefix, "creationtime desc")).Select(x => x.Id).ToList()
                .ShouldBe(Enumerable.Reverse(oldestFirst).ToList());
        }
    }

    [Fact]
    public async Task GetListAsync_WithAnUnrecognisedOrEmptySort_OrdersByDisplayOrderAscending()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var prefix = Token("sort-default");
            await SeedOneAsync(Guid.NewGuid(), $"{prefix}-a", displayOrder: 3);
            await SeedOneAsync(Guid.NewGuid(), $"{prefix}-b", displayOrder: 1);
            await SeedOneAsync(Guid.NewGuid(), $"{prefix}-c", displayOrder: 2);
            var byOrder = new List<string> { $"{prefix}-b", $"{prefix}-c", $"{prefix}-a" };

            // An unrecognised sort falls through to DisplayOrder ascending.
            (await ListAsync(prefix, "no-such-column")).Select(x => x.FieldLabel).ToList()
                .ShouldBe(byOrder);
            // An empty sort defaults to DisplayOrder.
            (await ListAsync(prefix, "")).Select(x => x.FieldLabel).ToList()
                .ShouldBe(byOrder);
            // So does a missing one.
            (await ListAsync(prefix, null)).Select(x => x.FieldLabel).ToList()
                .ShouldBe(byOrder);
        }
    }

    // ------------------------------------------------------------------------
    // Delete.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_SoftDeletesTheField()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var field = await CreateAsync(Token("delete"), Guid.NewGuid());

            await _fields.DeleteAsync(field.Id);

            await Should.ThrowAsync<EntityNotFoundException>(async () => await _fields.GetAsync(field.Id));
            await WithUnitOfWorkAsync(async () =>
            {
                using (_dataFilter.Disable<ISoftDelete>())
                {
                    (await _fieldRepository.GetAsync(field.Id)).IsDeleted.ShouldBeTrue(
                        "The field is SOFT-deleted, not removed.");
                }
            });
        }
    }
}
