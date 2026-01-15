using System.Net.Http.Json;
using FlowTime.Sim.Core.Services;
using FlowTime.Sim.Core.Templates;
using Microsoft.Extensions.DependencyInjection;

namespace FlowTime.Api.Tests;

public sealed class TemplateEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory factory;

    public TemplateEndpointsTests(TestWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task RefreshTemplates_ReturnsStatusPayload()
    {
        var clientFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<ITemplateService, StubTemplateService>();
            });
        });

        var client = clientFactory.CreateClient();
        var response = await client.PostAsync("/v1/templates/refresh", null);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<RefreshResponse>();
        Assert.NotNull(payload);
        Assert.Equal("refreshed", payload!.Status);
        Assert.Equal(3, payload.Templates);
    }

    private sealed record RefreshResponse(string Status, int Templates);

    private sealed class StubTemplateService : ITemplateService
    {
        public Task<IReadOnlyList<Template>> GetAllTemplatesAsync() => Task.FromResult<IReadOnlyList<Template>>(Array.Empty<Template>());
        public Task<Template?> GetTemplateAsync(string templateId) => Task.FromResult<Template?>(null);
        public Task<string> GenerateEngineModelAsync(string templateId, Dictionary<string, object> parameters, TemplateMode? modeOverride = null) => Task.FromResult(string.Empty);
        public Task<ValidationResult> ValidateParametersAsync(string templateId, Dictionary<string, object> parameters) => Task.FromResult(ValidationResult.Success());
        public Task<int> RefreshAsync() => Task.FromResult(3);
    }
}
