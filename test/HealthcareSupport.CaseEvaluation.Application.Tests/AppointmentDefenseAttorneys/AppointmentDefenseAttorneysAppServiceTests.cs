using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;

/// <summary>
/// Integration tests for <c>AppointmentDefenseAttorneysAppService</c>, the ABP-Suite
/// generated CRUD + lookup surface over the Appointment/DefenseAttorney join row.
///
/// <para><b>The seam.</b> The service itself is projections, three Guid.Empty guards and
/// four filtered lookups. The behaviour worth pinning sits one layer down in
/// <c>AppointmentDefenseAttorneyManager.CaptureSnapshotAsync</c>: linking (or re-pointing)
/// a defense attorney copies that master's name / firm / contact onto the Appointment as
/// a booking-time snapshot. That side effect is reachable ONLY through this service's
/// CreateAsync / UpdateAsync, and had no test before this file.</para>
///
/// <para><b>Fixture note.</b> There is no seeded DefenseAttorney master and no seeded
/// AppointmentDefenseAttorney join row in TestBase/Data, and both
/// <c>AppointmentId</c> and <c>DefenseAttorneyId</c> are enforced FKs under SQLite. Every
/// test therefore seeds its own master + appointment behind a per-test token and filters
/// only on ids it created, because the SQLite rig is shared and accumulates across the
/// whole run -- a count of rows this file did not create would be meaningless.</para>
///
/// <para><b>What these tests deliberately do NOT pin.</b></para>
/// <list type="bullet">
/// <item>Authorization. The test module calls <c>AddAlwaysAllowAuthorization()</c>, so the
/// four permissions (Default / Create / Edit / Delete) are unenforceable here and any
/// "access denied" assertion would be incapable of failing.</item>
/// <item><c>GetWithNavigationPropertiesAsync</c> against a missing id. The service
/// null-bangs the repository's null at line 55 and hands it to Mapperly; the resulting
/// exception type could not be established by reading alone, and guessing it would produce
/// a test that passes for an unknown reason.</item>
/// <item>The null-<c>IdentityUserId</c> projection. The entity's <c>IdentityUserId</c> is
/// <c>Guid?</c> while <c>AppointmentDefenseAttorneyDto.IdentityUserId</c> is a
/// non-nullable <c>Guid</c>; Mapperly's behaviour on that narrowing is not verifiable by
/// reading, so every link below is created with a real user id.</item>
/// <item><c>AppointmentDefenseAttorneyManager</c>'s <c>Check.NotNull(appointmentId, ...)</c>
/// calls (lines 33-34 and 43-44). The argument is a non-nullable <c>Guid</c>, so the check
/// can never throw. It is dead code, not an untested guard.</item>
/// </list>
///
/// <para>No local events, notifications or <c>ICurrentUser</c> reads exist on this service
/// or its manager, so there is nothing to hoist out of a unit-of-work lambda and no
/// impersonation is needed to avoid a vacuous internal-admin pass.</para>
/// </summary>
public abstract class AppointmentDefenseAttorneysAppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAppointmentDefenseAttorneysAppService _joinsAppService;
    private readonly IAppointmentDefenseAttorneyRepository _joinRepository;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly DefenseAttorneyManager _defenseAttorneyManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    protected AppointmentDefenseAttorneysAppServiceTests()
    {
        _joinsAppService = GetRequiredService<IAppointmentDefenseAttorneysAppService>();
        _joinRepository = GetRequiredService<IAppointmentDefenseAttorneyRepository>();
        _appointmentRepository = GetRequiredService<IRepository<Appointment, Guid>>();
        _defenseAttorneyManager = GetRequiredService<DefenseAttorneyManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _dataFilter = GetRequiredService<IDataFilter>();
    }

    // =====================================================================
    // A. Read surface -- GetAsync / GetWithNavigationPropertiesAsync / GetListAsync
    // =====================================================================

    [Fact]
    public async Task GetAsync_ReturnsTheLinkWithAllThreeForeignKeys()
    {
        var token = NewToken();
        var appointmentId = Guid.Empty;
        var masterId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                appointmentId = await SeedAppointmentAsync(token, "get");
                masterId = await SeedDefenseAttorneyAsync(token, "get");
            }
        });

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateLinkAsync(appointmentId, masterId, IdentityUsersTestData.DefenseAttorney1UserId);

            var fetched = await _joinsAppService.GetAsync(created.Id);

            fetched.ShouldNotBeNull();
            fetched.Id.ShouldBe(created.Id);
            fetched.AppointmentId.ShouldBe(appointmentId);
            fetched.DefenseAttorneyId.ShouldBe(masterId);
            fetched.IdentityUserId.ShouldBe(IdentityUsersTestData.DefenseAttorney1UserId);
        }
    }

    [Fact]
    public async Task GetAsync_WhenIdDoesNotExist_ThrowsEntityNotFound()
    {
        // The service calls the THROWING repository GetAsync (line 61), not FindAsync.
        // AppointmentsTestData exposes no NonExistentAppointmentDefenseAttorneyId, and a
        // fresh Guid is guaranteed not to collide with any accumulated row.
        var missingId = Guid.NewGuid();

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await Should.ThrowAsync<EntityNotFoundException>(
                async () => await _joinsAppService.GetAsync(missingId));
        }
    }

    [Fact]
    public async Task GetWithNavigationPropertiesAsync_HydratesAppointmentAttorneyAndUserNavs()
    {
        var token = NewToken();
        var appointmentId = Guid.Empty;
        var masterId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                appointmentId = await SeedAppointmentAsync(token, "nav");
                masterId = await SeedDefenseAttorneyAsync(token, "nav");
            }
        });

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateLinkAsync(appointmentId, masterId, IdentityUsersTestData.DefenseAttorney1UserId);

            var result = await _joinsAppService.GetWithNavigationPropertiesAsync(created.Id);

            result.ShouldNotBeNull();
            result.AppointmentDefenseAttorney.ShouldNotBeNull();
            result.AppointmentDefenseAttorney.Id.ShouldBe(created.Id);

            result.Appointment.ShouldNotBeNull();
            result.Appointment!.Id.ShouldBe(appointmentId);

            // FirmName, not just the id: it proves the DefenseAttorney row was really
            // joined and projected, rather than a shell carrying the FK back.
            result.DefenseAttorney.ShouldNotBeNull();
            result.DefenseAttorney!.Id.ShouldBe(masterId);
            result.DefenseAttorney!.FirmName.ShouldBe($"TEST-df-{token}-nav");

            result.IdentityUser.ShouldNotBeNull();
            result.IdentityUser!.Id.ShouldBe(IdentityUsersTestData.DefenseAttorney1UserId);
        }
    }

    [Fact]
    public async Task GetListAsync_FilteredByAppointmentId_ExcludesTheOtherAppointmentsLink()
    {
        // TWO appointments share ONE master, so the query is narrowed on BOTH ids and the
        // only clause that can produce the single-row answer is the appointmentId one.
        // Bounding the fixture this way also makes the count assertion safe on a shared,
        // accumulating rig: both candidate rows are rows this test created.
        var token = NewToken();
        var appointment1Id = Guid.Empty;
        var appointment2Id = Guid.Empty;
        var masterId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                appointment1Id = await SeedAppointmentAsync(token, "apt-one");
                appointment2Id = await SeedAppointmentAsync(token, "apt-two");
                masterId = await SeedDefenseAttorneyAsync(token, "apt");
            }
        });

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var link1 = await CreateLinkAsync(appointment1Id, masterId, IdentityUsersTestData.DefenseAttorney1UserId);
            var link2 = await CreateLinkAsync(appointment2Id, masterId, IdentityUsersTestData.DefenseAttorney1UserId);

            var result = await _joinsAppService.GetListAsync(new GetAppointmentDefenseAttorneysInput
            {
                AppointmentId = appointment1Id,
                DefenseAttorneyId = masterId
            });

            result.TotalCount.ShouldBe(1);
            result.Items.Count.ShouldBe(1);
            result.Items.Single().AppointmentDefenseAttorney.Id.ShouldBe(link1.Id);
            result.Items.Any(x => x.AppointmentDefenseAttorney.Id == link2.Id).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task GetListAsync_FilteredByDefenseAttorneyId_ExcludesTheOtherAttorneysLink()
    {
        // Mirror of the test above with the fixture inverted: ONE appointment, TWO masters.
        // The defenseAttorneyId clause is now the only one that can narrow two to one.
        var token = NewToken();
        var appointmentId = Guid.Empty;
        var master1Id = Guid.Empty;
        var master2Id = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                appointmentId = await SeedAppointmentAsync(token, "att");
                master1Id = await SeedDefenseAttorneyAsync(token, "att-one");
                master2Id = await SeedDefenseAttorneyAsync(token, "att-two");
            }
        });

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var link1 = await CreateLinkAsync(appointmentId, master1Id, IdentityUsersTestData.DefenseAttorney1UserId);
            var link2 = await CreateLinkAsync(appointmentId, master2Id, IdentityUsersTestData.DefenseAttorney1UserId);

            var result = await _joinsAppService.GetListAsync(new GetAppointmentDefenseAttorneysInput
            {
                AppointmentId = appointmentId,
                DefenseAttorneyId = master1Id
            });

            result.TotalCount.ShouldBe(1);
            result.Items.Count.ShouldBe(1);
            result.Items.Single().AppointmentDefenseAttorney.Id.ShouldBe(link1.Id);
            result.Items.Any(x => x.AppointmentDefenseAttorney.Id == link2.Id).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task GetListAsync_FilteredByIdentityUserId_ExcludesTheOtherUsersLink()
    {
        // Same fixture shape again, this time varying only the join row's IdentityUserId,
        // so the identityUserId clause is the sole thing separating the two rows.
        var token = NewToken();
        var appointmentId = Guid.Empty;
        var masterId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                appointmentId = await SeedAppointmentAsync(token, "usr");
                masterId = await SeedDefenseAttorneyAsync(token, "usr");
            }
        });

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var link1 = await CreateLinkAsync(appointmentId, masterId, IdentityUsersTestData.DefenseAttorney1UserId);
            var link2 = await CreateLinkAsync(appointmentId, masterId, IdentityUsersTestData.TenantAdmin1UserId);

            var result = await _joinsAppService.GetListAsync(new GetAppointmentDefenseAttorneysInput
            {
                AppointmentId = appointmentId,
                IdentityUserId = IdentityUsersTestData.DefenseAttorney1UserId
            });

            result.TotalCount.ShouldBe(1);
            result.Items.Count.ShouldBe(1);
            result.Items.Single().AppointmentDefenseAttorney.Id.ShouldBe(link1.Id);
            result.Items.Any(x => x.AppointmentDefenseAttorney.Id == link2.Id).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task GetListAsync_FilterText_IsIgnoredByTheGeneratedRepository()
    {
        // CHARACTERIZATION, not an endorsement. ABP Suite generated
        // EfCoreAppointmentDefenseAttorneyRepository.ApplyFilter with
        // `WhereIf(!string.IsNullOrWhiteSpace(filterText), e => true)` (lines 57 and 62),
        // i.e. FilterText reaches the query and matches everything. A text that matches no
        // column on any table still returns the row.
        //
        // If someone implements a real FilterText predicate this test SHOULD fail --
        // replace it with one asserting the new predicate, do not delete it.
        var token = NewToken();
        var appointmentId = Guid.Empty;
        var masterId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                appointmentId = await SeedAppointmentAsync(token, "txt");
                masterId = await SeedDefenseAttorneyAsync(token, "txt");
            }
        });

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateLinkAsync(appointmentId, masterId, IdentityUsersTestData.DefenseAttorney1UserId);

            var result = await _joinsAppService.GetListAsync(new GetAppointmentDefenseAttorneysInput
            {
                AppointmentId = appointmentId,
                FilterText = $"TEST-matches-no-column-{token}"
            });

            result.TotalCount.ShouldBe(1);
            result.Items.Single().AppointmentDefenseAttorney.Id.ShouldBe(created.Id);
        }
    }

    // =====================================================================
    // B. Tenant isolation
    // =====================================================================

    [Fact]
    public async Task GetListAsync_FromAnotherTenant_CannotSeeThisTenantsLink()
    {
        var token = NewToken();
        var appointmentId = Guid.Empty;
        var masterId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                appointmentId = await SeedAppointmentAsync(token, "iso");
                masterId = await SeedDefenseAttorneyAsync(token, "iso");
            }
        });

        AppointmentDefenseAttorneyDto created;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            created = await CreateLinkAsync(appointmentId, masterId, IdentityUsersTestData.DefenseAttorney1UserId);
        }

        using (_currentTenant.Change(TenantsTestData.TenantBRef))
        {
            var fromTenantB = await _joinsAppService.GetListAsync(new GetAppointmentDefenseAttorneysInput
            {
                AppointmentId = appointmentId
            });

            fromTenantB.TotalCount.ShouldBe(0);
            fromTenantB.Items.Any(x => x.AppointmentDefenseAttorney.Id == created.Id).ShouldBeFalse();
        }

        // POSITIVE CONTROL, load-bearing: without it, a link row that silently failed to
        // persist would satisfy the absence assertion above for entirely the wrong reason.
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var acrossTenants = await _joinsAppService.GetListAsync(new GetAppointmentDefenseAttorneysInput
            {
                AppointmentId = appointmentId
            });

            acrossTenants.Items.Any(x => x.AppointmentDefenseAttorney.Id == created.Id).ShouldBeTrue();
        }
    }

    // =====================================================================
    // C. Write surface -- guards, and the booking-time attorney snapshot
    // =====================================================================

    [Fact]
    public async Task CreateAsync_PersistsLink_AndCapturesTheAttorneySnapshotOntoTheAppointment()
    {
        // The reason this service is worth testing at all. AppointmentDefenseAttorneyManager
        // .CreateAsync line 37 calls CaptureSnapshotAsync, which copies the master's
        // name / firm / contact onto the Appointment so the booked-at values survive a later
        // master self-edit. Nothing else in the codebase exercises that path.
        var token = NewToken();
        var appointmentId = Guid.Empty;
        var masterId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                // Seeded with NO snapshot: the columns start null, so every asserted value
                // below can only have arrived via the capture.
                appointmentId = await SeedAppointmentAsync(token, "snap");
                masterId = await SeedDefenseAttorneyAsync(token, "snap");
            }
        });

        var before = await ReadAppointmentAsync(appointmentId);
        before.DefenseAttorneyFirmName.ShouldBeNull();

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await CreateLinkAsync(appointmentId, masterId, IdentityUsersTestData.DefenseAttorney1UserId);
        }

        // Re-read from a fresh unit of work so the assertion is on PERSISTED state rather
        // than on a change-tracked instance the service happened to leave behind.
        var after = await ReadAppointmentAsync(appointmentId);

        after.DefenseAttorneyFirstName.ShouldBe("TEST-Dana-snap");
        after.DefenseAttorneyLastName.ShouldBe("TEST-Synthetic-snap");
        after.DefenseAttorneyFirmName.ShouldBe($"TEST-df-{token}-snap");
        after.DefenseAttorneyCity.ShouldBe("TEST-City-snap");
        after.DefenseAttorneyPhoneNumber.ShouldBe("555-0100");
        after.DefenseAttorneyZipCode.ShouldBe("00000");
        after.DefenseAttorneyStateId.ShouldBe(LocationsTestData.State1Id);
    }

    [Fact]
    public async Task CreateAsync_WhenMasterIsInvisibleToTheTenant_LeavesTheSnapshotUntouched()
    {
        // The documented best-effort branch: AppointmentDefenseAttorneyManager lines 67-70
        // return early when either side of the snapshot cannot be resolved.
        //
        // The sentinels are LOAD-BEARING, not fixture noise. Against an appointment whose
        // snapshot columns are already null, "left untouched" and "overwritten with nulls"
        // are the same observation, so the test could not fail. Seeding real values first
        // is the only way the guarantee is provable.
        var token = NewToken();
        var sentinel = $"TEST-Keep-{token}";
        var appointmentId = Guid.Empty;
        var tenantBMasterId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                appointmentId = await SeedAppointmentAsync(token, "bestfx", sentinel);
            }
        });

        // The master lives in the OTHER tenant, so the manager's tenant-filtered FindAsync
        // returns null while the FK still resolves at the DB level (one shared SQLite
        // connection for the whole rig). Its own unit of work, so no DbContext is ever
        // asked to serve two tenants at once.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantBRef))
            {
                tenantBMasterId = await SeedDefenseAttorneyAsync(token, "bestfx");
            }
        });

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await CreateLinkAsync(appointmentId, tenantBMasterId, IdentityUsersTestData.DefenseAttorney1UserId);
        }

        var after = await ReadAppointmentAsync(appointmentId);

        after.DefenseAttorneyFirstName.ShouldBe(sentinel);
        after.DefenseAttorneyLastName.ShouldBe(sentinel);
        after.DefenseAttorneyFirmName.ShouldBe(sentinel);
        after.DefenseAttorneyCity.ShouldBe(sentinel);
    }

    [Fact]
    public async Task CreateAsync_WhenAppointmentIdIsEmpty_ThrowsUserFriendly()
    {
        // Reachable: AppointmentDefenseAttorneyCreateDto carries no DataAnnotations and the
        // Application assembly has exactly one FluentValidation AbstractValidator
        // (Appointments/Validators/AppointmentCreateDtoValidator.cs), which is unrelated.
        // Nothing runs before the service's own guard, so this really is the service's rule.
        var token = NewToken();
        var masterId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                masterId = await SeedDefenseAttorneyAsync(token, "g1");
            }
        });

        var input = new AppointmentDefenseAttorneyCreateDto
        {
            AppointmentId = Guid.Empty,
            DefenseAttorneyId = masterId,
            IdentityUserId = IdentityUsersTestData.DefenseAttorney1UserId
        };

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await Should.ThrowAsync<UserFriendlyException>(
                async () => await _joinsAppService.CreateAsync(input));
        }
    }

    [Fact]
    public async Task CreateAsync_WhenDefenseAttorneyIdIsEmpty_ThrowsUserFriendly()
    {
        // AppointmentId is deliberately VALID here, so the test also pins guard ORDER:
        // it can only pass if the second guard runs after the first one lets the call by.
        var token = NewToken();
        var appointmentId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                appointmentId = await SeedAppointmentAsync(token, "g2");
            }
        });

        var input = new AppointmentDefenseAttorneyCreateDto
        {
            AppointmentId = appointmentId,
            DefenseAttorneyId = Guid.Empty,
            IdentityUserId = IdentityUsersTestData.DefenseAttorney1UserId
        };

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await Should.ThrowAsync<UserFriendlyException>(
                async () => await _joinsAppService.CreateAsync(input));
        }
    }

    [Fact]
    public async Task CreateAsync_WhenIdentityUserIdIsEmpty_ThrowsUserFriendly()
    {
        // Both earlier ids valid, so only the third guard can produce the throw.
        var token = NewToken();
        var appointmentId = Guid.Empty;
        var masterId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                appointmentId = await SeedAppointmentAsync(token, "g3");
                masterId = await SeedDefenseAttorneyAsync(token, "g3");
            }
        });

        var input = new AppointmentDefenseAttorneyCreateDto
        {
            AppointmentId = appointmentId,
            DefenseAttorneyId = masterId,
            IdentityUserId = Guid.Empty
        };

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await Should.ThrowAsync<UserFriendlyException>(
                async () => await _joinsAppService.CreateAsync(input));
        }
    }

    [Fact]
    public async Task UpdateAsync_RepointsTheLink_AndReCapturesTheSnapshotToTheNewAttorney()
    {
        // The manager's docstring claims a single choke point that keeps the snapshot in
        // sync with appointment-side edits. This is the UPDATE half of that claim
        // (AppointmentDefenseAttorneyManager line 51) -- a distinct call site from the
        // create-side capture at line 37, so a distinct mutation kills it.
        var token = NewToken();
        var appointmentId = Guid.Empty;
        var master1Id = Guid.Empty;
        var master2Id = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                appointmentId = await SeedAppointmentAsync(token, "upd");
                master1Id = await SeedDefenseAttorneyAsync(token, "upd-one");
                master2Id = await SeedDefenseAttorneyAsync(token, "upd-two");
            }
        });

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateLinkAsync(appointmentId, master1Id, IdentityUsersTestData.DefenseAttorney1UserId);

            // Snapshot must currently be master ONE, otherwise the re-capture below would
            // be indistinguishable from the create-time capture.
            var afterCreate = await ReadAppointmentAsync(appointmentId);
            afterCreate.DefenseAttorneyFirmName.ShouldBe($"TEST-df-{token}-upd-one");

            // Read the persisted stamp rather than trusting the create DTO's copy.
            var persisted = await _joinRepository.GetAsync(created.Id);

            var updated = await _joinsAppService.UpdateAsync(created.Id, new AppointmentDefenseAttorneyUpdateDto
            {
                AppointmentId = appointmentId,
                DefenseAttorneyId = master2Id,
                IdentityUserId = IdentityUsersTestData.DefenseAttorney1UserId,
                ConcurrencyStamp = persisted.ConcurrencyStamp
            });

            updated.DefenseAttorneyId.ShouldBe(master2Id);
        }

        var afterUpdate = await ReadAppointmentAsync(appointmentId);
        afterUpdate.DefenseAttorneyFirmName.ShouldBe($"TEST-df-{token}-upd-two");
        afterUpdate.DefenseAttorneyFirstName.ShouldBe("TEST-Dana-upd-two");
        afterUpdate.DefenseAttorneyCity.ShouldBe("TEST-City-upd-two");
    }

    [Fact]
    public async Task UpdateAsync_WhenDefenseAttorneyIdIsEmpty_ThrowsUserFriendly()
    {
        // One representative guard on the update path (service lines 139-142). The other
        // two update guards are the same three-line shape as their create-side twins
        // already pinned above; pinning all six would be padding, not coverage.
        var token = NewToken();
        var appointmentId = Guid.Empty;
        var masterId = Guid.Empty;
        AppointmentDefenseAttorneyDto created;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                appointmentId = await SeedAppointmentAsync(token, "g4");
                masterId = await SeedDefenseAttorneyAsync(token, "g4");
            }
        });

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            created = await CreateLinkAsync(appointmentId, masterId, IdentityUsersTestData.DefenseAttorney1UserId);
        }

        var input = new AppointmentDefenseAttorneyUpdateDto
        {
            AppointmentId = appointmentId,
            DefenseAttorneyId = Guid.Empty,
            IdentityUserId = IdentityUsersTestData.DefenseAttorney1UserId,
            ConcurrencyStamp = created.ConcurrencyStamp
        };

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await Should.ThrowAsync<UserFriendlyException>(
                async () => await _joinsAppService.UpdateAsync(created.Id, input));
        }
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheLinkFromReads_AndLeavesTheAppointmentSnapshotIntact()
    {
        // Two guarantees, one of them negative. The snapshot is deliberately DECOUPLED from
        // the link: unlinking an attorney must not erase what was true at booking time.
        // That half is only provable because the create path populated the snapshot first --
        // against null columns the assertion would hold with the decoupling removed.
        var token = NewToken();
        var appointmentId = Guid.Empty;
        var masterId = Guid.Empty;
        AppointmentDefenseAttorneyDto created;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                appointmentId = await SeedAppointmentAsync(token, "del");
                masterId = await SeedDefenseAttorneyAsync(token, "del");
            }
        });

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            created = await CreateLinkAsync(appointmentId, masterId, IdentityUsersTestData.DefenseAttorney1UserId);

            var afterCreate = await ReadAppointmentAsync(appointmentId);
            afterCreate.DefenseAttorneyFirmName.ShouldBe($"TEST-df-{token}-del");

            await _joinsAppService.DeleteAsync(created.Id);
        }

        // Filter disabled so the row's absence is about DELETION, not about tenancy.
        using (_dataFilter.Disable<IMultiTenant>())
        {
            (await _joinRepository.FindAsync(created.Id)).ShouldBeNull();
        }

        var afterDelete = await ReadAppointmentAsync(appointmentId);
        afterDelete.DefenseAttorneyFirmName.ShouldBe($"TEST-df-{token}-del");
        afterDelete.DefenseAttorneyFirstName.ShouldBe("TEST-Dana-del");
    }

    // =====================================================================
    // D. Lookup endpoints
    // =====================================================================

    [Fact]
    public async Task GetDefenseAttorneyLookupAsync_FiltersOnFirmName_AndDisplaysIt()
    {
        // The seeded decoy is what makes the exclusion assertion real: with only one master
        // in the fixture, a filter on the wrong column would still return "my" row.
        var token = NewToken();
        var mineId = Guid.Empty;
        var otherId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                mineId = await SeedDefenseAttorneyAsync(token, "lk-mine");
                otherId = await SeedDefenseAttorneyAsync(token, "lk-other");
            }
        });

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _joinsAppService.GetDefenseAttorneyLookupAsync(new LookupRequestDto
            {
                Filter = $"TEST-df-{token}-lk-mine",
                MaxResultCount = 100
            });

            result.TotalCount.ShouldBe(1);
            result.Items.Count.ShouldBe(1);
            result.Items.Single().Id.ShouldBe(mineId);
            result.Items.Single().DisplayName.ShouldBe($"TEST-df-{token}-lk-mine");
            result.Items.Any(x => x.Id == otherId).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task GetDefenseAttorneyLookupAsync_FromAnotherTenant_DoesNotReturnThisTenantsAttorney()
    {
        // One practice must not see another practice's attorney roster.
        var token = NewToken();
        var tenantAMasterId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                tenantAMasterId = await SeedDefenseAttorneyAsync(token, "lk-iso");
            }
        });

        using (_currentTenant.Change(TenantsTestData.TenantBRef))
        {
            var fromTenantB = await _joinsAppService.GetDefenseAttorneyLookupAsync(new LookupRequestDto
            {
                Filter = $"TEST-df-{token}-lk-iso",
                MaxResultCount = 100
            });

            fromTenantB.TotalCount.ShouldBe(0);
            fromTenantB.Items.Any(x => x.Id == tenantAMasterId).ShouldBeFalse();
        }

        // POSITIVE CONTROL: proves the master really persisted, so the absence above is
        // tenancy rather than a failed seed.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var fromTenantA = await _joinsAppService.GetDefenseAttorneyLookupAsync(new LookupRequestDto
            {
                Filter = $"TEST-df-{token}-lk-iso",
                MaxResultCount = 100
            });

            fromTenantA.Items.Any(x => x.Id == tenantAMasterId).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task GetAppointmentLookupAsync_FiltersOnConfirmationNumber_AndReportsUnpagedTotal()
    {
        // Two things at once, both real:
        //  1. the filter is on RequestConfirmationNumber (the decoy uses a different infix);
        //  2. TotalCount comes from the UNPAGED query (service line 69 counts `query`, not
        //     `query.PageBy(...)`), so MaxResultCount = 1 must still report 2.
        // All three rows carry this test's token, so the count assertion is safe on a
        // shared rig that accumulates rows between tests.
        var token = NewToken();
        var match1Id = Guid.Empty;
        var match2Id = Guid.Empty;
        var decoyId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                match1Id = await SeedAppointmentAsync(token, "one");
                match2Id = await SeedAppointmentAsync(token, "two");
                decoyId = await SeedAppointmentAsync(token, "three", snapshotSentinel: null, rcnPrefix: "A9-ZZ");
            }
        });

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _joinsAppService.GetAppointmentLookupAsync(new LookupRequestDto
            {
                Filter = $"A9-DA-{token}",
                MaxResultCount = 1
            });

            result.TotalCount.ShouldBe(2);
            result.Items.Count.ShouldBe(1);
            result.Items.Single().Id.ShouldBeOneOf(match1Id, match2Id);
            result.Items.Single().DisplayName.ShouldStartWith($"A9-DA-{token}");
            result.Items.Any(x => x.Id == decoyId).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task GetIdentityUserLookupAsync_FiltersOnEmail_AndDisplaysIt()
    {
        // The email token and the username token are DELIBERATELY different. Without that
        // split, a mutation that filtered on UserName instead of Email would still find the
        // row and the test could not fail.
        var emailToken = NewToken();
        var userNameToken = NewToken();
        var mineId = Guid.Empty;
        var otherId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                mineId = await SeedLookupUserAsync(emailToken, userNameToken, "mine");
                otherId = await SeedLookupUserAsync(emailToken, userNameToken, "other");
            }
        });

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _joinsAppService.GetIdentityUserLookupAsync(new LookupRequestDto
            {
                Filter = $"TEST-du-{emailToken}-mine",
                MaxResultCount = 100
            });

            result.TotalCount.ShouldBe(1);
            result.Items.Count.ShouldBe(1);
            result.Items.Single().Id.ShouldBe(mineId);
            result.Items.Single().DisplayName.ShouldBe($"TEST-du-{emailToken}-mine@test.local");
            result.Items.Any(x => x.Id == otherId).ShouldBeFalse();
        }
    }

    // =====================================================================
    // Fixture helpers
    //
    // Every row these create carries a per-test token, because the SQLite rig is shared
    // across the whole run with no rollback between tests: only ids this file created are
    // safe to count or assert absence against.
    // =====================================================================

    private static string NewToken()
    {
        return Guid.NewGuid().ToString("N")[..8];
    }

    /// <summary>
    /// Creates one DefenseAttorney master in the CURRENT tenant, with every field the
    /// booking-time snapshot copies populated so the snapshot assertions have content to
    /// compare. IdentityUserId is left null: the FK is optional and the join row carries
    /// the user id the tests actually assert on.
    /// </summary>
    private async Task<Guid> SeedDefenseAttorneyAsync(string token, string suffix)
    {
        var master = await _defenseAttorneyManager.CreateAsync(
            stateId: LocationsTestData.State1Id,
            identityUserId: null,
            firmName: $"TEST-df-{token}-{suffix}",
            firmAddress: "TEST-100 Synthetic Way",
            phoneNumber: "555-0100",
            webAddress: "https://test.local/defense",
            faxNumber: "555-0101",
            street: "TEST-100 Synthetic Way",
            city: $"TEST-City-{suffix}",
            zipCode: "00000",
            // Unique per (tenant, email): the master table carries a filtered unique index
            // on (TenantId, Email), so a shared literal would collide across tests.
            email: $"TEST-df-{token}-{suffix}@test.local",
            firstName: $"TEST-Dana-{suffix}",
            lastName: $"TEST-Synthetic-{suffix}");

        return master.Id;
    }

    /// <summary>
    /// Seeds one TenantA appointment. The confirmation number embeds the token because
    /// (TenantId, RequestConfirmationNumber) is a unique index and the rig accumulates.
    /// Pass <paramref name="snapshotSentinel"/> to pre-populate the defense-attorney
    /// snapshot columns, which is what makes "the snapshot was left alone" provable.
    /// </summary>
    private async Task<Guid> SeedAppointmentAsync(
        string token,
        string suffix,
        string? snapshotSentinel = null,
        string rcnPrefix = "A9-DA")
    {
        var appointmentId = Guid.NewGuid();
        var appointment = new Appointment(
            id: appointmentId,
            patientId: PatientsTestData.Patient1Id,
            identityUserId: IdentityUsersTestData.TenantAdmin1UserId,
            appointmentTypeId: LocationsTestData.AppointmentType1Id,
            locationId: LocationsTestData.Location1Id,
            doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot1Id,
            appointmentDate: new DateTime(2027, 8, 1, 9, 0, 0, DateTimeKind.Utc),
            requestConfirmationNumber: $"{rcnPrefix}-{token}-{suffix}",
            appointmentStatus: AppointmentStatusType.Pending)
        {
            TenantId = TenantsTestData.TenantARef,
        };

        if (snapshotSentinel != null)
        {
            appointment.DefenseAttorneyFirstName = snapshotSentinel;
            appointment.DefenseAttorneyLastName = snapshotSentinel;
            appointment.DefenseAttorneyFirmName = snapshotSentinel;
            appointment.DefenseAttorneyCity = snapshotSentinel;
        }

        await _appointmentRepository.InsertAsync(appointment, autoSave: true);
        return appointmentId;
    }

    /// <summary>
    /// Creates one IdentityUser in the current tenant whose EMAIL embeds
    /// <paramref name="emailToken"/> and whose USERNAME embeds a different
    /// <paramref name="userNameToken"/>, so an email-filtered lookup and a
    /// username-filtered one cannot return the same answer.
    /// </summary>
    private async Task<Guid> SeedLookupUserAsync(string emailToken, string userNameToken, string suffix)
    {
        var userManager = GetRequiredService<IdentityUserManager>();
        var userId = Guid.NewGuid();
        var user = new IdentityUser(
            userId,
            $"TEST-un-{userNameToken}-{suffix}",
            $"TEST-du-{emailToken}-{suffix}@test.local",
            _currentTenant.Id);

        var result = await userManager.CreateAsync(user, IdentityUsersTestData.SeedPassword);
        result.Succeeded.ShouldBeTrue(
            "Seeding the lookup user failed, so this Fact would assert against a user that "
            + "does not exist: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        return userId;
    }

    private async Task<AppointmentDefenseAttorneyDto> CreateLinkAsync(Guid appointmentId, Guid defenseAttorneyId, Guid identityUserId)
    {
        return await _joinsAppService.CreateAsync(new AppointmentDefenseAttorneyCreateDto
        {
            AppointmentId = appointmentId,
            DefenseAttorneyId = defenseAttorneyId,
            IdentityUserId = identityUserId
        });
    }

    /// <summary>
    /// Re-reads the appointment in its own unit of work, so snapshot assertions run against
    /// what the database holds rather than against an instance still tracked by the context
    /// the service wrote through.
    /// </summary>
    private async Task<Appointment> ReadAppointmentAsync(Guid appointmentId)
    {
        return await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                return await _appointmentRepository.GetAsync(appointmentId);
            }
        });
    }
}
