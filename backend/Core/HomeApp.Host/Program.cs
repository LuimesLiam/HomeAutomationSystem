using HomeApp.AI;
using HomeApp.AI.Configuration;
using HomeApp.AI.Data;
using HomeApp.AI.Services;
using HomeApp.Expenses;
using HomeApp.Expenses.Configuration;
using HomeApp.Expenses.Data;
using HomeApp.Expenses.Services;
using HomeApp.Videos;
using HomeApp.Videos.Configuration;
using HomeApp.Videos.Data;
using HomeApp.Videos.Services;
using HomeApp.Host.Configuration;
using HomeApp.Host.Settings;
using HomeApp.SecuritySystem;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;

EnvFileLoader.LoadIntoEnvironment(Directory.GetCurrentDirectory());

var builder = WebApplication.CreateBuilder(args);
var hostOptions = HostEnvironmentOptions.FromConfiguration(builder.Configuration);
var videoOptions = VideoModuleOptions.FromConfiguration(builder.Configuration);
var aiOptions = AiModuleOptions.FromConfiguration(builder.Configuration);
var expenseOptions = ExpensesModuleOptions.FromConfiguration(builder.Configuration);
var securityOptions = SecuritySystemOptions.FromConfiguration(builder.Configuration);

MediaLibrarySettingsStore.ApplyPersistedSettings(builder.Environment.ContentRootPath, videoOptions);

builder.Services.AddSingleton(hostOptions);
builder.Services.AddSingleton<MediaLibrarySettingsStore>();

builder.Services.AddControllers();
builder.Services.AddHomeAppAi(builder.Configuration, aiOptions);
builder.Services.AddHomeAppSecuritySystem(builder.Configuration, securityOptions);
builder.Services.AddHomeAppVideos(builder.Configuration, videoOptions);
builder.Services.AddHomeAppExpenses(builder.Configuration, expenseOptions);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(HomeAppTelemetry.ServiceName))
    .WithTracing(tracing => tracing
        .AddSource(HomeAppTelemetry.ActivitySourceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        if (hostOptions.FrontendAllowedOrigins.Length > 0)
        {
            policy.WithOrigins(hostOptions.FrontendAllowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });

    options.AddPolicy("MediaStreaming", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .WithMethods("GET", "HEAD", "OPTIONS");
    });
});

var app = builder.Build();

try
{
    using var scope = app.Services.CreateScope();
    var videosDbContext = scope.ServiceProvider.GetRequiredService<HomeAppVideosDbContext>();
    videosDbContext.Database.Migrate();
    var aiDbContext = scope.ServiceProvider.GetRequiredService<HomeAppAiDbContext>();
    aiDbContext.Database.Migrate();
    var expensesDbContext = scope.ServiceProvider.GetRequiredService<HomeAppExpensesDbContext>();
    expensesDbContext.Database.Migrate();
    var mediaSourceService = scope.ServiceProvider.GetRequiredService<MediaSourceService>();
    await mediaSourceService.EnsureSeededAsync();
    var aiSettingsService = scope.ServiceProvider.GetRequiredService<AiSettingsService>();
    await aiSettingsService.EnsureSeededAsync();
    var expenseService = scope.ServiceProvider.GetRequiredService<ExpenseService>();
    await expenseService.EnsureSeededAsync();
}
catch (Exception ex) when (app.Environment.IsDevelopment())
{
    app.Logger.LogError(ex, "Database migration failed during startup. Continuing in Development so debugging can attach.");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

if (hostOptions.FrontendAllowedOrigins.Length > 0)
{
    app.UseCors("Frontend");
}

app.MapMetrics();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
