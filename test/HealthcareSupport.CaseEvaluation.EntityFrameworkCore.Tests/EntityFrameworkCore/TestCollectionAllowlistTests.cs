using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore.RealAuthorization;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

/// <summary>
/// Guard G7 (#1034). Keeps the EF Core test classes running in parallel by failing whenever a
/// test class joins an xUnit collection other than the two that must stay serial.
///
/// <para>Each EF Core test builds its own ABP application and its own in-memory SQLite database,
/// so xUnit's default of one collection per class is safe, and it is what lets a run use every
/// core. A <c>[Collection]</c> attribute puts every class that names it into one serial group.
/// Only two groups need that, because their NAMED shared-cache databases outlive a single test:
/// <see cref="MultiOfficeCollection"/> and <see cref="RealAuthorizationCollection"/>.</para>
///
/// <para>The old shared name is also blocked at compile time:
/// <see cref="CaseEvaluationTestConsts"/>'s <c>CollectionDefinitionName</c> is
/// <c>[Obsolete(error: true)]</c>. This test catches what the compiler cannot: that name written
/// as a string literal, or a new ad-hoc collection. Adding a serial group is a deliberate act, so
/// it means adding its name to <see cref="AllowedCollections"/> with a reason.</para>
///
/// <para>The Application and Domain test assemblies are scanned too, because their abstract test
/// bases are made concrete here, and an attribute on a base class would still apply.</para>
/// </summary>
public class TestCollectionAllowlistTests
{
    private static readonly HashSet<string> AllowedCollections = new(StringComparer.Ordinal)
    {
        MultiOfficeCollection.Name,
        RealAuthorizationCollection.Name,
    };

    [Fact]
    public void OnlyTheNamedSharedDatabaseCollectionsExist()
    {
        var offenders = TestAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type => CollectionNamesOn(type).Select(name => (type, name)))
            .Where(pair => !AllowedCollections.Contains(pair.name))
            .Select(pair => $"{pair.type.FullName} -> \"{pair.name}\"")
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToList();

        offenders.ShouldBeEmpty(
            "These test classes join an xUnit collection that is not allowlisted, which makes them run "
            + "one at a time again. Remove the [Collection]/[CollectionDefinition] attribute, or add the "
            + "name to AllowedCollections with a reason.");
    }

    [Fact]
    public void TheAllowlistedCollectionsAreStillInUse()
    {
        // Positive control: the scan must SEE the two collections that legitimately exist. If it
        // stopped reading attributes (a wrong assembly list, an attribute type mismatch), the test
        // above would pass on an empty result for the wrong reason.
        var seen = TestAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(CollectionNamesOn)
            .ToHashSet(StringComparer.Ordinal);

        seen.ShouldBe(AllowedCollections, ignoreOrder: true);
    }

    private static IEnumerable<Assembly> TestAssemblies() => new[]
    {
        typeof(TestCollectionAllowlistTests).Assembly,
        typeof(CaseEvaluationApplicationTestModule).Assembly,
        typeof(CaseEvaluationDomainTestModule).Assembly,
    }.Distinct();

    private static IEnumerable<string> CollectionNamesOn(Type type) =>
        type.GetCustomAttributesData()
            .Where(attribute => attribute.AttributeType == typeof(CollectionAttribute)
                || attribute.AttributeType == typeof(CollectionDefinitionAttribute))
            .Select(attribute => attribute.ConstructorArguments.Count > 0
                ? attribute.ConstructorArguments[0].Value?.ToString() ?? string.Empty
                : string.Empty);
}
