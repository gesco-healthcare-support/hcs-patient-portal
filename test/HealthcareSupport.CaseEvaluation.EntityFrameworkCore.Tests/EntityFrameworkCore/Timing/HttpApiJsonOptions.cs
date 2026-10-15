using System;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp.Json.SystemTextJson;
using Volo.Abp.Json.SystemTextJson.JsonConverters;
using Volo.Abp.Json.SystemTextJson.Modifiers;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Timing;

/// <summary>
/// The DateTime-relevant part of ABP's <c>MvcCoreBuilderExtensions.AddAbpJson</c> (ABP 10.0): the
/// JSON options the HTTP API actually writes responses with.
///
/// <para>WHY NOT <c>IJsonSerializer</c>. ABP's own serializer registers its DateTime converter with
/// <c>SkipDateTimeNormalization()</c>, so it never adds a Z and a wire test built on it passes
/// whatever the DTO says. The HTTP API registers the same converter WITHOUT the skip: every DTO
/// <c>DateTime</c> lacking <c>[DisableDateTimeNormalization]</c> goes through
/// <c>IClock.Normalize</c> and, with the clock pinned to Utc, leaves as UTC.</para>
/// </summary>
internal static class HttpApiJsonOptions
{
    public static JsonSerializerOptions Create(IServiceProvider services)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var resolver = new AbpDefaultJsonTypeInfoResolver(
            services.GetRequiredService<IOptions<AbpSystemTextJsonSerializerModifiersOptions>>());
        resolver.Modifiers.Add(
            new AbpDateTimeConverterModifier(
                    services.GetRequiredService<AbpDateTimeConverter>(),
                    services.GetRequiredService<AbpNullableDateTimeConverter>())
                .CreateModifyAction());
        options.TypeInfoResolver = resolver;
        return options;
    }
}
