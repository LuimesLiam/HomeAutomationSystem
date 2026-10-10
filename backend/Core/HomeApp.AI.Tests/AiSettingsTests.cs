using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HomeApp.AI.Controllers;
using HomeApp.AI.Data;
using HomeApp.AI.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace HomeApp.AI.Tests;

public sealed class AiSettingsTests
{
    private static LlmDto Model(string? parameters = null) => new()
    {
        Key = "local-model", Name = "Local model", ModelName = "test-model",
        Provider = "Local", BaseUrl = "http://localhost:11434/v1", ParamsJson = parameters,
        IsDefault = true
    };

    private static IHost Server(SqliteConnection connection) => new HostBuilder()
        .ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services =>
        {
            services.AddDbContext<HomeAppAiDbContext>(options => options.UseSqlite(connection));
            services.AddScoped<AiSettingsService>();
            services.AddControllers().AddApplicationPart(typeof(AiSettingsController).Assembly);
        }).Configure(app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        })).Start();

    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{\"temperature\":\"warm\"}")]
    [InlineData("{\"temperature\":1e999}")]
    [InlineData("{\"maxOutputTokens\":\"many\"}")]
    [InlineData("{\"maxOutputTokens\":1.5}")]
    [InlineData("{\"maxOutputTokens\":2147483648}")]
    [InlineData("{\"maxOutputTokens\":0}")]
    public async Task InvalidParametersReturn400AndPreserveSavedModels(string parameters)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var server = Server(connection);
        using (var scope = server.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<HomeAppAiDbContext>().Database.EnsureCreatedAsync();
        var client = server.GetTestClient();
        var saved = await client.PutAsJsonAsync("/api/settings/ai", new { llms = new[] { Model() } });
        saved.EnsureSuccessStatusCode();
        var before = await client.GetStringAsync("/api/settings/ai");
        var result = await client.PutAsJsonAsync("/api/settings/ai", new { llms = new[] { Model(parameters) } });
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        using var error = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(error.RootElement.GetProperty("message").GetString()));
        Assert.Equal(before, await client.GetStringAsync("/api/settings/ai"));
    }

    [Theory]
    [InlineData("{\"llms\":null}")]
    [InlineData("{\"llms\":[null]}")]
    [InlineData("{\"llms\":[]}")]
    [InlineData("{\"llms\":[{}]}")]
    public async Task InvalidListsReturn400(string json)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        using var server = Server(connection);
        var result = await server.GetTestClient().PutAsync("/api/settings/ai", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("file:///tmp/model")]
    [InlineData("https://user:password@example.com/v1")]
    [InlineData("https://example.com/v1?token=example")]
    [InlineData("https://example.com/v1#fragment")]
    public async Task InvalidEndpointsReturn400(string endpoint)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        using var server = Server(connection);
        var model = Model(); model.BaseUrl = endpoint;
        var result = await server.GetTestClient().PutAsJsonAsync("/api/settings/ai", new { llms = new[] { model } });
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task IncompleteOrDuplicateRowsDoNotSilentlyDisappear()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        using var server = Server(connection);
        var client = server.GetTestClient();
        foreach (var rows in new[] { new[] { Model(), new LlmDto() }, new[] { Model(), Model() } })
        {
            var result = await client.PutAsJsonAsync("/api/settings/ai", new { llms = rows });
            Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        }
    }

    [Fact]
    public async Task ValidParametersAndLocalHttpEndpointSaveAndResolve()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var server = Server(connection);
        using var scope = server.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<HomeAppAiDbContext>().Database.EnsureCreatedAsync();
        var response = await server.GetTestClient().PutAsJsonAsync("/api/settings/ai",
            new { llms = new[] { Model("{\"temperature\":0.5,\"maxOutputTokens\":1200}") } });
        response.EnsureSuccessStatusCode();
        var model = await scope.ServiceProvider.GetRequiredService<AiSettingsService>().GetResolvedModelAsync("local-model");
        Assert.Equal(0.5, model.Temperature);
        Assert.Equal(1200, model.MaxOutputTokens);
        Assert.Equal("http://localhost:11434/v1/", model.BaseUrl);
    }
}
