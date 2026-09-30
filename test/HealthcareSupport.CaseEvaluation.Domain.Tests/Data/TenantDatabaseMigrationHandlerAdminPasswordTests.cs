using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Identity.AdminPasswords;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Data;

/// <summary>
/// B12 follow-up -- where the tenant migration handler may and may not write an admin-password
/// entry.
///
/// <para>The handler runs in every process, but only the DbMigrator provisions anything. It used to
/// resolve the store BEFORE checking which process it was in, so the API wrote an entry for every
/// office it created or re-pointed, holding a password no account was given. These assert on what
/// the store HOLDS afterwards.</para>
/// </summary>
public sealed class TenantDatabaseMigrationHandlerAdminPasswordTests
{
    private const string StoredPassword = "Synthetic-Stored-Pass6!";

    private static readonly Guid Office = new("0ff1ce00-0000-4000-8000-00000000e0f1");

    private static (CaseEvaluationTenantDatabaseMigrationHandler Handler, RecordingStore Store, IOfficeDatabaseProvisioner Provisioner)
        Build(string applicationName)
    {
        var store = new RecordingStore(StoredPassword);
        var provisioner = Substitute.For<IOfficeDatabaseProvisioner>();
        var handler = new CaseEvaluationTenantDatabaseMigrationHandler(
            provisioner,
            new StubHostEnvironment(applicationName),
            store,
            NullLogger<CaseEvaluationTenantDatabaseMigrationHandler>.Instance);
        return (handler, store, provisioner);
    }

    private static TenantCreatedEto Created(string? typedPassword = null)
    {
        var eto = new TenantCreatedEto { Id = Office, Name = "TEST-office" };
        if (typedPassword != null)
        {
            eto.Properties["AdminPassword"] = typedPassword;
        }

        return eto;
    }

    [Fact]
    public async Task InTheApi_CreatingAnOffice_WritesNoStoreEntry()
    {
        var (handler, store, _) = Build("HealthcareSupport.CaseEvaluation.HttpApi.Host");

        await handler.HandleEventAsync(Created());

        store.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task InTheApi_ApplyingMigrations_WritesNoStoreEntry()
    {
        var (handler, store, _) = Build("HealthcareSupport.CaseEvaluation.HttpApi.Host");

        await handler.HandleEventAsync(new ApplyDatabaseMigrationsEto { TenantId = Office, DatabaseName = "Default" });

        store.Entries.ShouldBeEmpty();
    }

    /// <summary>
    /// The refusal still happens in the API, which is where offices are created. Moving the store
    /// call later must not have moved this check with it.
    /// </summary>
    [Fact]
    public async Task InTheApi_ATypedPublishedDefault_IsStillRefused()
    {
        var (handler, store, _) = Build("HealthcareSupport.CaseEvaluation.HttpApi.Host");

        var refused = await Should.ThrowAsync<BusinessException>(() =>
            handler.HandleEventAsync(Created(CaseEvaluationConsts.AdminPasswordDefaultValue)));

        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AdminPasswordIsAKnownDefault);
        store.Entries.ShouldBeEmpty();
    }

    /// <summary>The positive control: the migrator provisions with the stored password.</summary>
    [Fact]
    public async Task InTheMigrator_CreatingAnOfficeWithNothingTyped_ProvisionsWithTheStoredPassword()
    {
        var (handler, store, provisioner) = Build("HealthcareSupport.CaseEvaluation.DbMigrator");

        await handler.HandleEventAsync(Created());

        store.Entries.ShouldContainKey(AdminPasswordNames.For(Office));
        await provisioner.Received(1).ProvisionAsync(Office, Arg.Any<string>(), StoredPassword);
    }

    /// <summary>A typed password is kept as typed, and the store is not consulted for it.</summary>
    [Fact]
    public async Task InTheMigrator_ATypedPassword_IsUsedAndNothingIsStored()
    {
        const string typed = "Synthetic-Typed-Pass7!";
        var (handler, store, provisioner) = Build("HealthcareSupport.CaseEvaluation.DbMigrator");

        await handler.HandleEventAsync(Created(typed));

        store.Entries.ShouldBeEmpty();
        await provisioner.Received(1).ProvisionAsync(Office, Arg.Any<string>(), typed);
    }

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

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public StubHostEnvironment(string applicationName)
        {
            ApplicationName = applicationName;
        }

        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; }

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
