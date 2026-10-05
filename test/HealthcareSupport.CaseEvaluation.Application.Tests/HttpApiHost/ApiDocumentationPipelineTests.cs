using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ApiDocumentation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.HttpApiHost.Tests;

/// <summary>
/// The API documentation (OpenAPI document plus UI) was served unauthenticated in every environment.
/// It is now Development only. These tests drive the real pipeline, so a request for the document is
/// what is asserted, not merely a boolean.
/// </summary>
public class ApiDocumentationPipelineTests
{
    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Theory]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    [InlineData("", false)]
    [InlineData("Development", true)]
    public void ShouldServe_is_true_only_in_Development(string environment, bool expected)
    {
        ApiDocumentationPipeline.ShouldServe(new FakeEnvironment(environment)).ShouldBe(expected);
    }

    private static async Task<int> RequestAsync(string environment, string path)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRouting();
        services.AddControllers();
        services.AddSingleton<IHostEnvironment>(new FakeEnvironment(environment));
        services.AddSingleton<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(
            new FakeWebEnvironment(environment));
        services.AddSwaggerGen();
        var provider = services.BuildServiceProvider();

        var app = new ApplicationBuilder(provider);
        ApiDocumentationPipeline.Apply(app, new FakeEnvironment(environment), "client");
        // Anything the documentation middleware does not answer ends here.
        app.Run(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Path = path;
        context.Request.Method = "GET";
        context.Response.Body = new System.IO.MemoryStream();
        await app.Build()(context);
        return context.Response.StatusCode;
    }

    private sealed class FakeWebEnvironment(string name) : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = ".";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task The_document_and_the_UI_are_not_served_outside_Development(string environment)
    {
        (await RequestAsync(environment, "/swagger/v1/swagger.json")).ShouldBe(404);
        (await RequestAsync(environment, "/swagger/index.html")).ShouldBe(404);
    }

    [Fact]
    public async Task The_document_is_served_in_Development()
    {
        (await RequestAsync("Development", "/swagger/v1/swagger.json")).ShouldBe(200);
    }
}
