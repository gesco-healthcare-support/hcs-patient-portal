using System.Text.Json;
using Shouldly;
using Volo.Abp.Data;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Extensions;

/// <summary>
/// Pins <see cref="ExtraPropertyConverters"/>. Item D (2026-08-22) added the int pair for the
/// progressive-lockout cycle counter, which lives in <c>AbpUsers.ExtraProperties</c> precisely so it
/// needs no migration.
///
/// <para>The file's own docstring called it "a single, tested helper" -- it had no tests. These cover
/// both pairs, with the <see cref="JsonElement"/> cases first, because that is the entire reason the
/// helper exists: ABP's typed <c>GetProperty&lt;T&gt;</c> throws on a freshly reloaded entity whose
/// value came back through the JSON column (ABP issues 12547 / 19430 / 23546).</para>
///
/// <para>Pure unit -- no DB, no ABP DI.</para>
/// </summary>
public class ExtraPropertyConvertersTests
{
    private sealed class Bag : IHasExtraProperties
    {
        public ExtraPropertyDictionary ExtraProperties { get; } = new();
    }

    /// <summary>Round-trips through JSON so the value really is a JsonElement, not a boxed int.</summary>
    private static JsonElement JsonOf(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    // ------------------------------------------------------------------ CoerceInt

    [Fact]
    public void CoerceInt_ReadsAJsonNumber()
    {
        ExtraPropertyConverters.CoerceInt(JsonOf("3")).ShouldBe(3);
    }

    [Fact]
    public void CoerceInt_ReadsANumericJsonString()
    {
        ExtraPropertyConverters.CoerceInt(JsonOf("\"4\"")).ShouldBe(4);
    }

    [Fact]
    public void CoerceInt_FallsBackForANonNumericJsonValue()
    {
        ExtraPropertyConverters.CoerceInt(JsonOf("true"), 7).ShouldBe(7);
        ExtraPropertyConverters.CoerceInt(JsonOf("\"banana\""), 7).ShouldBe(7);
        ExtraPropertyConverters.CoerceInt(JsonOf("null"), 7).ShouldBe(7);
    }

    [Fact]
    public void CoerceInt_ReadsNativeIntAndLong()
    {
        ExtraPropertyConverters.CoerceInt(5).ShouldBe(5);
        ExtraPropertyConverters.CoerceInt(6L).ShouldBe(6);
    }

    [Fact]
    public void CoerceInt_TreatsAnOutOfRangeLongAsAbsentRatherThanTruncating()
    {
        // A silently wrapped value would pick the wrong backoff rung, which is worse than falling
        // back to the default and starting the ladder over.
        ExtraPropertyConverters.CoerceInt(long.MaxValue, 2).ShouldBe(2);
        ExtraPropertyConverters.CoerceInt(long.MinValue, 2).ShouldBe(2);
    }

    [Fact]
    public void CoerceInt_ReadsNumericStringsAndRejectsTheRest()
    {
        ExtraPropertyConverters.CoerceInt("8").ShouldBe(8);
        ExtraPropertyConverters.CoerceInt("", 1).ShouldBe(1);
        ExtraPropertyConverters.CoerceInt("not a number", 1).ShouldBe(1);
    }

    [Fact]
    public void CoerceInt_ReturnsTheDefaultForNull()
    {
        ExtraPropertyConverters.CoerceInt(null, 9).ShouldBe(9);
        ExtraPropertyConverters.CoerceInt(null).ShouldBe(0);
    }

    // ------------------------------------------------------------------ GetIntOrDefault

    [Fact]
    public void GetIntOrDefault_ReadsAStoredValue()
    {
        var bag = new Bag();
        bag.ExtraProperties["LockoutCycle"] = 2;

        ExtraPropertyConverters.GetIntOrDefault(bag, "LockoutCycle").ShouldBe(2);
    }

    [Fact]
    public void GetIntOrDefault_ReadsAValueThatCameBackAsJson()
    {
        // The reload case the helper exists for.
        var bag = new Bag();
        bag.ExtraProperties["LockoutCycle"] = JsonOf("3");

        ExtraPropertyConverters.GetIntOrDefault(bag, "LockoutCycle").ShouldBe(3);
    }

    [Fact]
    public void GetIntOrDefault_ReturnsTheDefaultWhenAbsentOrUnusable()
    {
        var bag = new Bag();

        ExtraPropertyConverters.GetIntOrDefault(bag, "Missing", 4).ShouldBe(4);
        ExtraPropertyConverters.GetIntOrDefault(null, "LockoutCycle", 4).ShouldBe(4);
        ExtraPropertyConverters.GetIntOrDefault(bag, "", 4).ShouldBe(4);
    }

    // ------------------------------------------------------------------ CoerceBool (previously untested)

    [Fact]
    public void CoerceBool_ReadsJsonTrueFalseAndStrings()
    {
        ExtraPropertyConverters.CoerceBool(JsonOf("true")).ShouldBeTrue();
        ExtraPropertyConverters.CoerceBool(JsonOf("false")).ShouldBeFalse();
        ExtraPropertyConverters.CoerceBool(JsonOf("\"True\"")).ShouldBeTrue();

        // A JSON number is NOT a bool here: asserted both ways so this proves the default is
        // returned rather than passing by coincidence on a one-sided assertion.
        ExtraPropertyConverters.CoerceBool(JsonOf("1"), true).ShouldBeTrue();
        ExtraPropertyConverters.CoerceBool(JsonOf("1"), false).ShouldBeFalse();
    }

    [Fact]
    public void CoerceBool_ReadsNativeAndStringValues()
    {
        ExtraPropertyConverters.CoerceBool(true).ShouldBeTrue();
        ExtraPropertyConverters.CoerceBool("true").ShouldBeTrue();
        ExtraPropertyConverters.CoerceBool("TRUE").ShouldBeTrue();
        ExtraPropertyConverters.CoerceBool("nope").ShouldBeFalse();
        ExtraPropertyConverters.CoerceBool(null, true).ShouldBeTrue();
    }
}
