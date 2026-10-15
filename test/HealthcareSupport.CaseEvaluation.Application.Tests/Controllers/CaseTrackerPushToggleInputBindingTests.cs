using System.Text.Json;
using HealthcareSupport.CaseEvaluation.Controllers.Integration;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Controllers;

/// <summary>
/// The push toggle's body must name its value. A bool with no marker binds a missing or
/// misspelled key to <c>false</c>, so <c>{}</c> switched an office's Case Tracker push OFF and
/// reported success. The web defaults below are the ones ASP.NET Core's JSON input formatter
/// starts from; a deserialisation failure there becomes a model-state error and a 400.
/// </summary>
public class CaseTrackerPushToggleInputBindingTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"enable\":true}")]
    [InlineData("{\"isEnabled\":false}")]
    public void A_body_without_enabled_is_refused_rather_than_read_as_off(string body)
    {
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<CaseTrackerPushToggleInput>(body, WebOptions));
    }

    [Theory]
    [InlineData("{\"enabled\":true}", true)]
    [InlineData("{\"enabled\":false}", false)]
    public void A_body_naming_enabled_binds_its_value(string body, bool expected)
    {
        JsonSerializer.Deserialize<CaseTrackerPushToggleInput>(body, WebOptions)!.Enabled.ShouldBe(expected);
    }
}
