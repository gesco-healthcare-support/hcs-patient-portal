using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Patients;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.MultiTenancy;

/// <summary>
/// Records, for every Domain entity, three observations that together say where its rows live
/// and whether ABP scopes them to one office, as a deterministic, sorted table.
///
/// WHY THIS EXISTS. Until 2026-09-28 docs/architecture/MULTI-TENANCY.md listed seven
/// office-scoped entities (Location, State, WcabOffice, AppointmentType, AppointmentStatus,
/// AppointmentLanguage, NotificationTemplateType) as host-only reference data shared by every
/// office. The code had moved on months earlier, and code was being reasoned about from stale
/// documents: the T3 lookup-scope comment in AppointmentsAppService records that it once
/// justified a filter with "Patient is not IMultiTenant per CLAUDE.md", which was false. A table
/// generated from the code cannot drift that way.
///
/// THE THREE OBSERVATIONS, recorded side by side and never combined:
///   - IMultiTenant: whether the class implements the interface ABP's data filter keys on.
///   - CaseEvaluationDbContext: whether the host model maps it, i.e. whether the host database
///     (Migrations/) has its table.
///   - CaseEvaluationTenantDbContext: whether the office model maps it, i.e. whether every office
///     database (TenantMigrations/) has its table.
///
/// WHY THEY ARE NOT COMBINED INTO A VERDICT. "Host-only" cannot be read off the interface. Five
/// join entities carry no IMultiTenant and still exist in every office database; one entity
/// class has no table anywhere. The stale page reached its inverted list by exactly that kind of
/// inference, so a generator that inferred one column from another would reproduce the error
/// and hide it. The design rule this repository adopted from AuthorizationSurface applies:
/// record the raw inputs, never a derived conclusion.
///
/// WHAT THIS DOES NOT PROVE:
///   - That ABP's filter actually confines a query to one office. The multi-office isolation
///     tests (MultiOffice/MultiOfficeIsolationMatrixTests) do that.
///   - That a deployed database matches the model. The migrations do, and CI's
///     has-pending-model-changes gate holds each migration snapshot to its model.
///   - Which configuration block put an entity into a model: the shared configuration, or the
///     host's IsHostDatabase() block.
///   - How a request reaches an office's database.
/// It proves only that none of the three observations changes without someone saying so.
/// </summary>
public static class TenancySurface
{
    /// <summary>Rendered when a model maps the entity, so its database has the table.</summary>
    public const string Mapped = "mapped";

    /// <summary>Rendered when a model does not map the entity.</summary>
    public const string Absent = "absent";

    /// <summary>The assembly whose entities are recorded; Patient is only an anchor type.</summary>
    public static Assembly DomainAssembly => typeof(Patient).Assembly;

    /// <summary>The Domain types the host model maps.</summary>
    public static IReadOnlySet<Type> HostModelTypes()
    {
        using var context = new CaseEvaluationDbContext(ModelOnlyOptions<CaseEvaluationDbContext>());
        return MappedDomainTypes(context, DomainAssembly);
    }

    /// <summary>The Domain types the office model maps.</summary>
    public static IReadOnlySet<Type> OfficeModelTypes()
    {
        using var context = new CaseEvaluationTenantDbContext(ModelOnlyOptions<CaseEvaluationTenantDbContext>());
        return MappedDomainTypes(context, DomainAssembly);
    }

    /// <summary>The CLR types from <paramref name="domain"/> that <paramref name="context"/>'s model maps.</summary>
    public static IReadOnlySet<Type> MappedDomainTypes(DbContext context, Assembly domain)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(domain);

        return context.Model.GetEntityTypes()
            .Select(entityType => entityType.ClrType)
            .Where(type => type.Assembly == domain)
            .ToHashSet();
    }

    /// <summary>
    /// Every concrete entity class declared in <paramref name="domain"/>, plus any Domain type
    /// either model maps, sorted ordinal by full name.
    ///
    /// The union is deliberate. Reading only the models would silently drop an entity class no
    /// database has, and reading only the classes would drop a mapped type that is not an
    /// entity. Either would hide a row the reader needs. Ordinal, as in AuthorizationSurface, so
    /// the file is byte-identical on every machine.
    /// </summary>
    public static IReadOnlyList<Type> Entities(Assembly domain, IReadOnlySet<Type> host, IReadOnlySet<Type> office)
    {
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(office);

        return domain.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters)
            .Where(type => typeof(IEntity).IsAssignableFrom(type))
            .Concat(host)
            .Concat(office)
            .Distinct()
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The whole surface from the real models, LF-terminated.</summary>
    public static string Render()
    {
        return Render(DomainAssembly, HostModelTypes(), OfficeModelTypes());
    }

    /// <summary>
    /// One line per entity, LF-terminated: the full name, then the three observations.
    ///
    /// LF is fixed rather than Environment.NewLine so the committed file is the same bytes on
    /// Windows and in the Linux CI container.
    /// </summary>
    public static string Render(Assembly domain, IReadOnlySet<Type> host, IReadOnlySet<Type> office)
    {
        var builder = new StringBuilder();

        foreach (var entity in Entities(domain, host, office))
        {
            builder.Append(entity.FullName)
                   .Append(" -> IMultiTenant=")
                   .Append(typeof(IMultiTenant).IsAssignableFrom(entity) ? "yes" : "no")
                   .Append(' ').Append(nameof(CaseEvaluationDbContext)).Append('=')
                   .Append(host.Contains(entity) ? Mapped : Absent)
                   .Append(' ').Append(nameof(CaseEvaluationTenantDbContext)).Append('=')
                   .Append(office.Contains(entity) ? Mapped : Absent)
                   .Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>
    /// Options that let a context build its model and nothing else. The connection string is
    /// never opened: reading Model needs a provider, not a database. SQLite is this project's
    /// test provider, and which entities a model contains does not depend on the provider.
    /// </summary>
    private static DbContextOptions<TContext> ModelOnlyOptions<TContext>()
        where TContext : DbContext
    {
        return new DbContextOptionsBuilder<TContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
    }
}
