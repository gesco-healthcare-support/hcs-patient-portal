using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Pdf;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>The sidecar client with a fake handler: the render call and the template-version call.</summary>
public class WeasyPrintPacketRendererTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.Method + " " + request.RequestUri!.PathAndQuery);
            return Task.FromResult(respond(request));
        }
    }

    private static (WeasyPrintPacketRenderer Renderer, FakeHandler Handler) Build(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new FakeHandler(respond);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://renderer.invalid") };
        return (new WeasyPrintPacketRenderer(client, NullLogger<WeasyPrintPacketRenderer>.Instance), handler);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task GetTemplateVersion_ReturnsTheSha256_AndAsksForThatTemplate()
    {
        var (renderer, handler) = Build(_ => Json(HttpStatusCode.OK, "{\"template\":\"patient-blank\",\"sha256\":\"abc123\"}"));

        var version = await renderer.GetTemplateVersionAsync("patient-blank");

        version.ShouldBe("abc123");
        handler.Requests.ShouldBe(["GET /template-version/patient-blank"]);
    }

    [Fact]
    public async Task GetTemplateVersion_ThrowsOnANonSuccessStatus() =>
        await Should.ThrowAsync<HttpRequestException>(
            () => Build(_ => Json(HttpStatusCode.NotFound, "{}")).Renderer.GetTemplateVersionAsync("nope"));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetTemplateVersion_RefusesABlankTemplateName(string name) =>
        await Should.ThrowAsync<ArgumentException>(
            () => Build(_ => Json(HttpStatusCode.OK, "{}")).Renderer.GetTemplateVersionAsync(name));

    [Theory]
    [InlineData("{\"sha256\":\"\"}")]
    [InlineData("{\"sha256\":null}")]
    public async Task GetTemplateVersion_ThrowsWhenTheSidecarReturnsNoVersion(string body) =>
        await Should.ThrowAsync<InvalidOperationException>(
            () => Build(_ => Json(HttpStatusCode.OK, body)).Renderer.GetTemplateVersionAsync("doctor"));

    [Fact]
    public async Task Render_PostsTheTemplateAndTokens_AndReturnsThePdf()
    {
        string? posted = null;
        var (renderer, _) = Build(request =>
        {
            posted = request.Content!.ReadAsStringAsync().Result;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([37, 80, 68, 70]) };
        });

        var pdf = await renderer.RenderAsync("doctor-blank", new Dictionary<string, string> { ["##Office.Phone##"] = "555" });

        pdf.ShouldBe([37, 80, 68, 70]);
        posted.ShouldNotBeNull().ShouldContain("doctor-blank");
        posted.ShouldContain("##Office.Phone##");
    }

    [Fact]
    public async Task Render_ThrowsOnAFailedRender() =>
        await Should.ThrowAsync<HttpRequestException>(
            () => Build(_ => Json(HttpStatusCode.InternalServerError, "{\"error\":\"x\"}"))
                .Renderer.RenderAsync("doctor", new Dictionary<string, string>()));

    [Fact]
    public async Task Render_RefusesABlankTemplateName() =>
        await Should.ThrowAsync<ArgumentException>(
            () => Build(_ => Json(HttpStatusCode.OK, "{}")).Renderer.RenderAsync(" ", new Dictionary<string, string>()));
}
