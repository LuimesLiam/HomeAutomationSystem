using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HomeApp.Commute;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace HomeApp.Commute.Tests;

public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
public sealed class FakeRoadProvider : IRoadTrafficProvider
{
    public bool Configured { get; set; } = true;
    public bool Fail { get; set; }
    public int Calls;
    public RoadItem[] ExtraItems { get; set; } = [];
    public Task<TrafficFeed> FetchAsync(string resource, CancellationToken ct)
    {
        Calls++; if (Fail) throw new HttpRequestException("offline");
        var near = new GeoPoint(.01,.01);
        var cameras = resource == "cameras" ? new TrafficCamera[] {
            new("2", "End camera", "A1", "Eastbound", new(.02,.02), [new("v2", "https://example.com/2.jpg", "View two", true)]),
            new("1", "Start camera", "A1", "Westbound", new(0,0), [new("v1", "https://example.com/1.jpg", "View one", true)]),
            new("3", "Remote camera", "A1", "Eastbound", new(10,10), []),
            new("4", "Other road", "A2", "Eastbound", near, []) } : [];
        var item = new RoadItem("closure", "incident", "Full closure", "All lanes blocked", "A1", "Eastbound", near,
            [near], null, null, null, true, "All lanes", "");
        return Task.FromResult(new TrafficFeed(cameras, resource == "incidents" ? new[] { item }.Concat(ExtraItems).ToArray() : []));
    }
}
public sealed class FakeMarineProvider(TestClock clock) : IMarineTrafficProvider
{
    public bool Fail; public int Calls;
    public Task<MarineFeed> FetchAsync(MarineBounds b, CancellationToken ct)
    {
        Calls++; if (Fail) throw new HttpRequestException("offline");
        return Task.FromResult(new MarineFeed([CommuteTests.Approach(clock.Now)], ["Synthetic receiver"], false));
    }
}
public sealed class CommuteTests
{
    [Fact]
    public async Task SeparateReportsWithMatchingDescriptionsRetainClosuresAndSchedules()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var clock = new TestClock();
        var position = new GeoPoint(.01, .01);
        var advisory = new RoadItem("advisory", "incident", "Lane restriction", "Repeated provider description",
            "A1", "Eastbound", position, [position], clock.Now, null, null, false, "One lane", "");
        var closure = advisory with { Id = "separate-closure", FullClosure = true, EndsAt = clock.Now.AddHours(2) };
        var upcoming = advisory with { Id = "scheduled-event", StartsAt = clock.Now.AddDays(1) };
        var roads = new FakeRoadProvider { ExtraItems = [advisory, closure, upcoming, advisory] };
        using var server = Server(connection, roads, new(clock), clock, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        await server.Services.GetRequiredService<RouteSettingsStore>().SaveAsync(SyntheticRoute(), default);
        var service = server.Services.GetRequiredService<CommuteService>();
        await service.PollAsync(default);

        var reports = service.Snapshot().Items.Where(item => item.Description == advisory.Description).ToArray();
        Assert.Equal(3, reports.Length);
        Assert.Contains(reports, item => item.Id == closure.Id && item.FullClosure);
        Assert.Contains(reports, item => item.Id == upcoming.Id && item.StartsAt == upcoming.StartsAt);
    }

    public static readonly BridgeConfiguration TestBridge = new("Synthetic bridge", new(0,0),225);
    public static CommuteRoute SyntheticRoute() => new() {
        Id = Guid.NewGuid(), Name = "Synthetic route", Origin = "Start", Destination = "End", Roadways = ["A1"],
        Points = [new(0,0),new(.01,.01),new(.02,.02)], AlertRegions = [], CorridorKm = .5,
        MarineBounds = new(-.1,-.1,.1,.1), Bridge = TestBridge
    };
    public static Vessel Approach(DateTimeOffset time) => new("123456789", "TEST SHIP", new(.01,.01),
        225,225,8,70,"Cargo",120,20,"TEST PORT",time,"fixture",0,true);
    [Fact]
    public void PredictionUsesSavedBridgeRejectsUncertainReportsAndAllowsBothDirections()
    {
        var clock = new TestClock(); var predictor = new LiftBridgePredictor(); var v = Approach(clock.Now);
        var inbound = predictor.Predict(v,TestBridge,clock.Now);
        Assert.Equal("Inbound",inbound.Direction); Assert.Equal("IMMINENT",inbound.Level);
        Assert.True(inbound.WindowStart <= inbound.WindowEnd); Assert.InRange(inbound.Confidence,15,85);
        var outbound = predictor.Predict(v with { Position = new(-.01,-.01), Course = 45, Heading = 45 },TestBridge,clock.Now);
        Assert.Equal("Outbound",outbound.Direction); Assert.Equal("IMMINENT",outbound.Level);
        foreach (var candidate in new[] { v with { SeenAt = clock.Now.AddMinutes(-6) }, v with { SpeedKnots = 0 },
            v with { Course = null }, v with { Course = 90 }, v with { NavigationStatus = 5 },
            v with { PositionFreshnessKnown = false }, v with { SeenAt = clock.Now.AddMinutes(3) } })
        { var p = predictor.Predict(candidate,TestBridge,clock.Now); Assert.Equal("LOW",p.Level); Assert.Null(p.WindowStart); }
        Assert.Equal("POSSIBLE",predictor.Predict(v with { TypeCode = 37, LengthMetres = 8 },TestBridge,clock.Now).Level);
        Assert.Equal("LOW",predictor.Predict(v,TestBridge with { Position = new(10,10) },clock.Now).Level);
    }
    [Fact]
    public void GeometryFiltersDistantSegmentsDatesDirectionsAndRegionalAlerts()
    {
        var route = SyntheticRoute(); var clock = new TestClock();
        Assert.True(RouteGeometry.Relevant([new(-1,.01),new(1,.01)],route));
        Assert.False(RouteGeometry.Relevant([new(10,10),new(11,11)],route));
        Assert.Empty(RouteGeometry.Decode("bad"));
        Assert.False(CommuteService.RoadMatches("A11",route));
        var item = new RoadItem("x","incident","x","x","A1","Eastbound",new(0,0),[new(0,0)],null,null,null,false,"","");
        Assert.True(CommuteService.Relevant(item,route,clock.Now));
        Assert.False(CommuteService.Relevant(item with { Direction = "Westbound" },route with { Direction = "Eastbound" },clock.Now));
        Assert.False(CommuteService.Relevant(item with { EndsAt = clock.Now.AddSeconds(-1) },route,clock.Now));
        Assert.False(CommuteService.Relevant(item with { StartsAt = clock.Now.AddDays(8) },route,clock.Now));
        Assert.False(CommuteService.Relevant(item with { Regional = true, Roadway = "Synthetic region" },route,clock.Now));
        Assert.True(CommuteService.Relevant(item with { Regional = true, Roadway = "Synthetic region" },route with { AlertRegions = ["Synthetic region"] },clock.Now));
    }
    [Theory]
    [InlineData("cameras", "/custom/api/v2/get/cameras")]
    [InlineData("incidents", "/custom/api/v2/get/event")]
    [InlineData("construction", "/custom/api/v2/get/constructionprojects")]
    [InlineData("conditions", "/custom/api/v3/get/roadconditions")]
    [InlineData("alerts", "/custom/api/v2/get/alerts")]
    public async Task RoadProviderRequiresConfiguredEndpointAndUsesItsApiPrefix(string resource, string expectedPath)
    {
        using var http = new HttpClient(new CaptureHandler());
        foreach (var endpoint in new[] { "", "http://example.com/api", "https://example.com/api?key=other", "https://user@example.com/api" })
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                ["ROAD_TRAFFIC_API_KEY"] = "fixture-key", ["ROAD_TRAFFIC_API_BASE_URL"] = endpoint
            }).Build();
            var provider = new RoadTrafficProvider(http, configuration);
            Assert.False(provider.Configured);
            await Assert.ThrowsAsync<InvalidOperationException>(() => provider.FetchAsync("alerts", default));
        }
        var handler = new CaptureHandler();
        using var configuredHttp = new HttpClient(handler);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ROAD_TRAFFIC_API_KEY"] = "fixture-key", ["ROAD_TRAFFIC_API_BASE_URL"] = "https://example.com/custom/api/"
        }).Build();
        var configured = new RoadTrafficProvider(configuredHttp, config);
        Assert.True(configured.Configured);
        await configured.FetchAsync(resource, default);
        Assert.Equal(expectedPath, handler.RequestUri!.AbsolutePath);
        Assert.Contains("key=fixture-key", handler.RequestUri.Query);
    }
    private sealed class CaptureHandler : HttpMessageHandler
    {
        public Uri? RequestUri;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("[]") });
        }
    }
    [Fact]
    public void ParsesDocumentedSourceFormatsAndAisSentinelsWithoutLocationPresets()
    {
        using var road = JsonDocument.Parse("""[{"ID":123,"RoadwayName":"A1","Description":"Collision","EventType":"accidentsAndIncidents","IsFullClosure":true,"Latitude":0,"Longitude":0,"LastUpdated":1767268800}]""");
        var feed = RoadTrafficProvider.Parse("incidents",road.RootElement); Assert.True(feed.Items[0].FullClosure); Assert.NotNull(feed.Items[0].UpdatedAt);
        using var camera = JsonDocument.Parse("""[{"Id":12,"Roadway":"A1","Latitude":0,"Longitude":0,"Views":[{"Id":1,"Url":"https://example.com/1.jpg","Status":"Enabled"},{"Id":2,"Url":"javascript:alert(1)","Status":"Enabled"}]}]""");
        Assert.Single(RoadTrafficProvider.Parse("cameras",camera.RootElement).Cameras[0].Views);
        using var ais = JsonDocument.Parse("""{"features":[{"id":123456789,"geometry":{"type":"Point","coordinates":[0,0]},"properties":{"name":"TEST","type":70,"cog":360,"heading":511,"sog":102.3,"seen":"2026-01-01T12:00:00Z","kind":"vessel","msg_type":"ShipStaticData"}}],"attribution":{"receiver":"Synthetic credit"}}""");
        var vessel = Assert.Single(OpenWatersProvider.Parse(ais.RootElement).Vessels);
        Assert.Null(vessel.Course); Assert.Null(vessel.Heading); Assert.Null(vessel.SpeedKnots); Assert.False(vessel.PositionFreshnessKnown);
        Assert.Empty(new CommuteRoute().Points); Assert.Empty(new CommuteRoute().Roadways); Assert.Null(new CommuteRoute().MarineBounds); Assert.Null(new CommuteRoute().Bridge);
    }
    private static IHost Server(SqliteConnection connection, FakeRoadProvider roads, FakeMarineProvider marine, TestClock clock, string? privateDirectory, string? contentRoot = null)
    {
        return new HostBuilder().ConfigureWebHost(web => web.UseContentRoot(contentRoot ?? Directory.GetCurrentDirectory()).UseTestServer()
            .ConfigureAppConfiguration(c => c.AddInMemoryCollection(new Dictionary<string,string?> { ["POSTGRES_CONNECTION"] = "Host=localhost;Database=unused", ["Commute:PrivateMigrationsPath"] = privateDirectory }))
            .ConfigureServices(s => {
                s.AddHomeAppCommute(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["POSTGRES_CONNECTION"] = "Host=localhost;Database=unused" }).Build());
                s.RemoveAll<IHostedService>(); s.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<CommuteDbContext>>(); s.RemoveAll<DbContextOptions<CommuteDbContext>>(); s.RemoveAll<CommuteDbContext>();
                s.AddDbContext<CommuteDbContext>(o => o.UseSqlite(connection));
                s.AddSingleton<IRoadTrafficProvider>(roads); s.AddSingleton<IMarineTrafficProvider>(marine); s.AddSingleton<TimeProvider>(clock);
            }).Configure(app => { app.UseRouting(); app.UseEndpoints(e => e.MapControllers()); })).Start();
    }
    [Fact]
    public async Task RoutesPersistAcrossRestartReorderCamerasAndRetainDegradedCache()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var clock = new TestClock(); var roads = new FakeRoadProvider(); var marine = new FakeMarineProvider(clock);
        using (var server = Server(connection,roads,marine,clock,Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString())))
        {
            var client = server.GetTestClient(); var route = SyntheticRoute();
            Assert.Equal(HttpStatusCode.OK,(await client.PutAsJsonAsync("/api/commute/route",route)).StatusCode);
            var service = server.Services.GetRequiredService<CommuteService>(); await service.PollAsync(default);
            var snapshot = (await client.GetFromJsonAsync<CommuteSnapshot>("/api/commute"))!;
            Assert.Equal("DISRUPTED",snapshot.Status); Assert.Equal(new[] { "1", "2" },snapshot.Cameras.Select(c => c.Id));
            Assert.Contains(snapshot.Predictions,p => p.Level == "IMMINENT");
            await Task.WhenAll(Enumerable.Range(0,10).Select(_ => service.PollAsync(default)));
            Assert.Equal(5,roads.Calls); Assert.Equal(1,marine.Calls);
            var reverse = route with { Name = "Synthetic return", Points = route.Points.Reverse().ToArray() };
            Assert.Equal(HttpStatusCode.OK,(await client.PutAsJsonAsync("/api/commute/route",reverse)).StatusCode);
            Assert.Equal(new[] { "2", "1" },service.Snapshot().Cameras.Select(c => c.Id));
            Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync("/api/commute/route",reverse with { Points = [] })).StatusCode);
            roads.Fail = true; marine.Fail = true; clock.Now = clock.Now.AddMinutes(3); await service.PollAsync(default);
            var stale = service.Snapshot(); Assert.All(stale.Feeds,f => Assert.Equal("stale",f.State)); Assert.Empty(stale.Predictions); Assert.Equal(2,stale.Cameras.Length);
            roads.Fail = false; marine.Fail = false; clock.Now = clock.Now.AddMinutes(3); await service.PollAsync(default);
            Assert.All(service.Snapshot().Feeds,f => Assert.Equal("ready",f.State));
        }
        using var restarted = Server(connection,roads,marine,clock,Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString()));
        await restarted.Services.GetRequiredService<RouteSettingsStore>().InitializeAsync(default);
        Assert.Equal("Synthetic return",restarted.Services.GetRequiredService<RouteSettingsStore>().Current!.Name);
    }
    [Fact]
    public async Task DashboardFavoritesOrderingMapsAndDeletionAreDatabaseBacked()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var clock = new TestClock(); using var server = Server(connection,new(),new(clock),clock,Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString()));
        var client = server.GetTestClient(); var route = SyntheticRoute(); await client.PutAsJsonAsync("/api/commute/route",route);
        var profile = new DashboardProfile { Name = "Custom view", RouteIds = [route.Id], FavoriteViews = [new("1","v1")], Panels = ["cameras","map"], FavoritesOnly = true };
        Assert.Equal(HttpStatusCode.OK,(await client.PutAsJsonAsync("/api/commute/dashboards",profile)).StatusCode);
        var workspace = (await client.GetFromJsonAsync<WorkspaceDto>("/api/commute/workspace"))!;
        var saved = Assert.Single(workspace.Dashboards); Assert.Equal(new[] { "cameras","map" },saved.Panels); Assert.True(saved.FavoritesOnly); Assert.Single(saved.FavoriteViews);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync("/api/commute/dashboards",profile with { Panels = ["unknown"] })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync("/api/commute/dashboards",profile with { RouteIds = [Guid.NewGuid()] })).StatusCode);
        await client.PutAsJsonAsync("/api/commute/maps/key",new GoogleMapsKeyRequest("test-key"));
        var map = (await client.GetFromJsonAsync<RouteMaps>("/api/commute/maps"))!;
        Assert.StartsWith("https://www.google.com/maps/dir/?api=1",map.DirectionsUrl); Assert.Contains("waypoints=",map.DirectionsUrl);
        Assert.StartsWith("https://www.google.com/maps/embed/v1/directions?",map.EmbedUrl); Assert.Contains("key=test-key",map.EmbedUrl);
        var publicResponse = await client.GetAsync("/api/commute/workspace");
        Assert.Contains("no-store",publicResponse.Headers.CacheControl!.ToString());
        var publicJson = await publicResponse.Content.ReadAsStringAsync(); Assert.DoesNotContain("test-key",publicJson); Assert.Contains("googleMapsConfigured",publicJson);
        await client.PutAsJsonAsync("/api/commute/maps/key",new GoogleMapsKeyRequest("")); Assert.Null((await client.GetFromJsonAsync<RouteMaps>("/api/commute/maps"))!.EmbedUrl);
        await client.DeleteAsync("/api/commute/routes/"+route.Id);
        var after = (await client.GetFromJsonAsync<WorkspaceDto>("/api/commute/workspace"))!;
        Assert.Empty(after.Routes); Assert.Empty(after.Dashboards[0].RouteIds); Assert.Null(after.ActiveRouteId);
        await client.DeleteAsync("/api/commute/dashboards/"+saved.Id);
        Assert.Empty((await client.GetFromJsonAsync<WorkspaceDto>("/api/commute/workspace"))!.Dashboards);
    }
    [Fact]
    public async Task EmptyDatabaseHasNoImplicitLocationAndMissingKeyCannotReportClear()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var clock = new TestClock(); var roads = new FakeRoadProvider { Configured = false }; var marine = new FakeMarineProvider(clock);
        using var server = Server(connection,roads,marine,clock,Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString()));
        var store = server.Services.GetRequiredService<RouteSettingsStore>(); await store.InitializeAsync(default);
        Assert.Empty(store.Workspace.Routes); Assert.Null(store.Current);
        var service = server.Services.GetRequiredService<CommuteService>(); await service.PollAsync(default);
        Assert.Equal(0,roads.Calls); Assert.Equal(0,marine.Calls); Assert.Null(service.Snapshot().Route);
        await store.SaveAsync(SyntheticRoute(),default); await service.PollAsync(default);
        Assert.Equal("UNKNOWN",service.Snapshot().Status); Assert.All(service.Snapshot().Feeds.Where(f => f.Id != "marine"),f => Assert.Equal("unconfigured",f.State));
        await store.SaveAsync(store.Current! with { MarineBounds = null, Bridge = null },default);
        Assert.Empty(service.Snapshot().Predictions); Assert.Empty(service.Snapshot().Vessels);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReverseTripPersistsDirectionEndpointsAndRefiltersTheSameCorridor(bool longLabels)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var clock = new TestClock(); var directory = Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());
        var original = SyntheticRoute() with { Direction = "Eastbound" };
        if (longLabels) original = original with { Origin = new string('A',120), Destination = new string('B',120) };
        using (var host = Server(connection,new(),new(clock),clock,directory))
        {
            var client = host.GetTestClient();
            await client.PutAsJsonAsync("/api/commute/route",original);
            var service = host.Services.GetRequiredService<CommuteService>(); await service.PollAsync(default);
            Assert.Single(service.Snapshot().Items);
            var response = await client.PutAsJsonAsync("/api/commute/route/reverse",new ReverseRouteRequest(original.Id));
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            var route = (await client.GetFromJsonAsync<CommuteRoute>("/api/commute/route"))!;
            Assert.Equal(original.Destination,route.Origin); Assert.Equal(original.Origin,route.Destination);
            Assert.Equal("Westbound",route.Direction); Assert.Equal(original.Points.Reverse(),route.Points);
            Assert.Null(RouteSettingsStore.Validate(route));
            Assert.Equal(original.Bridge,route.Bridge); Assert.Equal(original.MarineBounds,route.MarineBounds);
            Assert.Equal(new[] { "2", "1" },service.Snapshot().Cameras.Select(c => c.Id));
            Assert.Empty(service.Snapshot().Items);
            Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync("/api/commute/route/reverse",new ReverseRouteRequest(Guid.NewGuid()))).StatusCode);
        }
        using var restarted = Server(connection,new(),new(clock),clock,directory);
        var restartedClient = restarted.GetTestClient();
        await restarted.Services.GetRequiredService<RouteSettingsStore>().InitializeAsync(default);
        Assert.Equal("Westbound",(await restartedClient.GetFromJsonAsync<CommuteRoute>("/api/commute/route"))!.Direction);
        await restartedClient.PutAsJsonAsync("/api/commute/route/reverse",new ReverseRouteRequest(original.Id));
        var restored = (await restartedClient.GetFromJsonAsync<CommuteRoute>("/api/commute/route"))!;
        Assert.Equal(original.Origin,restored.Origin); Assert.Equal(original.Destination,restored.Destination);
        Assert.Equal(original.Points,restored.Points); Assert.Equal("Eastbound",restored.Direction);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrivateSqlMigrationsApplyOnceAndDoNotOverwriteLaterChanges(bool discoverDevcontainer)
    {
        var directory = Path.Combine(Path.GetTempPath(),"commute-private-"+Guid.NewGuid()); Directory.CreateDirectory(directory);
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        try
        {
            var workspace = new CommuteWorkspace { Routes = [SyntheticRoute()] }; workspace = workspace with { ActiveRouteId = workspace.Routes[0].Id };
            var json = JsonSerializer.Serialize(workspace,new JsonSerializerOptions(JsonSerializerDefaults.Web)).Replace("'","''");
            var seedDirectory = discoverDevcontainer ? Path.Combine(directory,".devcontainer","private-migrations","commute") : directory;
            Directory.CreateDirectory(seedDirectory);
            await File.WriteAllTextAsync(Path.Combine(seedDirectory,"001-test-seed.sql"),$"INSERT INTO \"CommuteWorkspaces\" (\"Id\",\"Json\",\"Revision\") VALUES (1,'{json}',1);");
            var clock = new TestClock();
            using (var host = Server(connection,new(),new(clock),clock,discoverDevcontainer ? null : directory,discoverDevcontainer ? directory : null))
            {
                var store = host.Services.GetRequiredService<RouteSettingsStore>(); await store.InitializeAsync(default);
                Assert.True(store.Available); Assert.Equal("Synthetic route",store.Current!.Name);
                await store.SaveAsync(store.Current with { Name = "Edited after seed" },default);
            }
            using var restarted = Server(connection,new(),new(clock),clock,discoverDevcontainer ? null : directory,discoverDevcontainer ? directory : null);
            var again = restarted.Services.GetRequiredService<RouteSettingsStore>(); await again.InitializeAsync(default);
            Assert.Equal("Edited after seed",again.Current!.Name);
        }
        finally { Directory.Delete(directory,true); }
    }
}
