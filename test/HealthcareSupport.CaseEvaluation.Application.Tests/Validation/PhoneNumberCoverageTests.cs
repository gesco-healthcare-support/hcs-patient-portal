using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Validation;
using Shouldly;
using System.Linq;
using System.Reflection;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Validation;

/// <summary>
/// Every phone or fax property on an INPUT DTO must carry <see cref="PhoneNumberAttribute"/>.
///
/// <para>WHY A REFLECTION TEST. The Angular field masks input to ten digits, but a mask is a
/// convenience, not a control -- anything posting to the API directly goes straight past it. So the
/// attribute is the only thing that actually enforces the format, and the way it gets lost is not
/// someone deleting it: it is someone adding a new party type, copying a nearby DTO, and not
/// knowing the rule exists. Nothing about that fails a build or a normal test. This does, and it
/// names the property.</para>
///
/// <para>SCOPE, and why it is not "every phone property everywhere". OUTPUT DTOs are excluded
/// because validating on the way out would break the screen that DISPLAYS a legacy value rather
/// than the form that enters one -- there are stored seven-digit numbers and numbers with
/// extensions, and a patient's record should not fail to load because of how a phone number was
/// typed years ago. <c>Get*Input</c> phone properties are excluded because they are SEARCH FILTERS,
/// and partial-number search has to keep working.</para>
/// </summary>
public class PhoneNumberCoverageTests
{
    private static readonly Assembly ContractsAssembly = typeof(PatientCreateDto).Assembly;

    /// <summary>Mirrors the rule used when the attribute was rolled out, so the two cannot drift.</summary>
    private static bool IsInputDto(System.Type type)
    {
        var name = type.Name;
        if (name.StartsWith("Get", System.StringComparison.Ordinal))
        {
            return false;
        }

        var isCreateOrUpdate =
            name.Contains("Create", System.StringComparison.Ordinal)
            || name.Contains("Update", System.StringComparison.Ordinal);

        return isCreateOrUpdate
            && (name.EndsWith("Dto", System.StringComparison.Ordinal)
                || name.EndsWith("Input", System.StringComparison.Ordinal));
    }

    private static bool IsPhoneProperty(PropertyInfo property)
    {
        if (property.PropertyType != typeof(string))
        {
            // PhoneNumberTypeId is an enum (Work / Home) -- a selector, not a number.
            return false;
        }

        return property.Name.Contains("Phone", System.StringComparison.Ordinal)
            || property.Name.Contains("Fax", System.StringComparison.Ordinal);
    }

    [Fact]
    public void EveryPhonePropertyOnAnInputDtoIsValidated()
    {
        var unguarded = ContractsAssembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && IsInputDto(type))
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(IsPhoneProperty)
                .Where(property => property.GetCustomAttribute<PhoneNumberAttribute>() == null)
                .Select(property => $"{type.Name}.{property.Name}"))
            .OrderBy(name => name, System.StringComparer.Ordinal)
            .ToList();

        unguarded.ShouldBeEmpty(
            "These phone/fax properties are on input DTOs but carry no [PhoneNumber], so the API " +
            "will accept any text the column can hold. Add [PhoneNumber] (from " +
            "HealthcareSupport.CaseEvaluation.Validation). If one of these is genuinely not a " +
            "phone number, rename it -- the check is by name because that is what a reviewer reads " +
            "too. Unguarded: " + string.Join("; ", unguarded));
    }

    [Fact]
    public void TheCheckActuallyFindsProperties()
    {
        // Guards the guard: if the naming rule or the assembly reference ever stops matching, the
        // test above would pass by finding nothing at all and the coverage claim would be empty.
        var guarded = ContractsAssembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && IsInputDto(type))
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(IsPhoneProperty))
            .Count();

        guarded.ShouldBeGreaterThan(
            25,
            "The reflection scan found almost no phone properties on input DTOs, which means the " +
            "naming rule or the assembly reference has drifted -- not that the app stopped having " +
            "phone fields. 33 were annotated when this shipped (2026-08-27).");
    }
}
