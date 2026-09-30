using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Identity.AdminPasswords;
using Shouldly;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Identity;

/// <summary>
/// B12 follow-up -- a store entry is written only when the seeder is about to CREATE the admin.
///
/// <para>Before this, the migrator asked the store for every database on every run, so a database
/// whose admin already existed got an entry holding a password the account never had. These assert
/// on what the store ends up HOLDING, against the real IdentityUserManager, rather than on whether
/// a method was called.</para>
/// </summary>
public abstract class AdminSeedPasswordResolverTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string StoredPassword = "Synthetic-Stored-Pass4!";

    private readonly IdentityUserManager _userManager;

    protected AdminSeedPasswordResolverTests()
    {
        _userManager = GetRequiredService<IdentityUserManager>();
    }

    private async Task GivenAnAdminExistsAsync()
    {
        if (await _userManager.FindByNameAsync(CaseEvaluationConsts.AdminUserName) != null)
        {
            return;
        }

        var admin = new IdentityUser(Guid.NewGuid(), CaseEvaluationConsts.AdminUserName, "c3d4e5f6@a7b8c9d0.com");
        (await _userManager.CreateAsync(admin, "Synthetic-Own-Pass5!")).Succeeded.ShouldBeTrue();
    }

    /// <summary>A tenant id no user belongs to: the state of a database about to get its admin.</summary>
    private static readonly Guid AnOfficeWithNoUsers = new("0ff1ce00-0000-4000-8000-00000000f0a1");

    /// <summary>
    /// The defect: an existing admin used to leave an entry behind. Now the store stays empty, so
    /// no file exists that an operator could hand over as that account's password.
    /// </summary>
    [Fact]
    public async Task WhenTheAdminAlreadyExists_NothingIsWrittenToTheStore()
    {
        await GivenAnAdminExistsAsync();
        var store = new RecordingStore(StoredPassword);

        var password = await new AdminSeedPasswordResolver(_userManager, store).ResolveAsync(null);

        store.Entries.ShouldBeEmpty();
        password.ShouldNotBe(StoredPassword);
        AdminPasswordPolicy.IsKnownDefault(password)
            .ShouldBeFalse("a published default must never reach the seed context, even unused");
    }

    /// <summary>The positive control: a database about to get its admin uses the stored value.</summary>
    [Fact]
    public async Task WhenNoAdminExists_TheStoredPasswordIsUsedAndKept()
    {
        var store = new RecordingStore(StoredPassword);
        string password;

        using (GetRequiredService<Volo.Abp.MultiTenancy.ICurrentTenant>().Change(AnOfficeWithNoUsers))
        {
            (await _userManager.FindByNameAsync(CaseEvaluationConsts.AdminUserName))
                .ShouldBeNull("the fixture needs a scope with no admin in it");

            password = await new AdminSeedPasswordResolver(_userManager, store).ResolveAsync(AnOfficeWithNoUsers);
        }

        password.ShouldBe(StoredPassword);
        store.Entries.ShouldContainKey(AdminPasswordNames.For(AnOfficeWithNoUsers));
    }

    /// <summary>An in-memory store that records what it holds, keyed like the real ones.</summary>
    private sealed class RecordingStore : IAdminPasswordStore
    {
        private readonly string _password;

        public RecordingStore(string password)
        {
            _password = password;
        }

        public Dictionary<string, string> Entries { get; } = new();

        public Task<string> GetOrCreateAsync(Guid? tenantId, CancellationToken cancellationToken = default)
        {
            var name = AdminPasswordNames.For(tenantId);
            if (!Entries.TryGetValue(name, out var stored))
            {
                stored = _password;
                Entries[name] = stored;
            }

            return Task.FromResult(stored);
        }
    }
}
