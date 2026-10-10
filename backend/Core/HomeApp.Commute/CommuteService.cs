using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;

namespace HomeApp.Commute;

public sealed class CommuteService(RouteSettingsStore settings, IRoadTrafficProvider traffic,
    IMarineTrafficProvider marine, ILiftBridgePredictor predictor, TimeProvider clock)
{
    private readonly object _sync = new();
    private readonly SemaphoreSlim _pollGate = new(1);
    private readonly Dictionary<string, TrafficFeed> _traffic = new();
    private MarineFeed _marine = new([], [], false);
    private MarineBounds? _marineBounds;
    private readonly Dictionary<string, FeedStatus> _feeds = new[]
    {
        ("cameras", "Road cameras"), ("incidents", "Road incidents"), ("construction", "Road construction"),
        ("conditions", "Road conditions"), ("alerts", "Regional alerts"), ("marine", "Open Waters AIS")
    }.ToDictionary(x => x.Item1, x => new FeedStatus(x.Item1, x.Item2, "waiting", null, null, null, "Waiting for first refresh."));

    public async Task PollAsync(CancellationToken ct)
    {
        if (!await _pollGate.WaitAsync(0, ct)) return;
        try
        {
            await settings.InitializeAsync(ct);
            if (!settings.Available || settings.Current is null) return;
            // One cycle per process, shared by every browser. Route edits only re-filter the cache.
            await Task.WhenAll(PollTrafficAsync(ct), PollMarineAsync(ct));
        }
        finally { _pollGate.Release(); }
    }
    private async Task PollTrafficAsync(CancellationToken ct)
    {
        foreach (var id in new[] { "incidents", "construction", "conditions", "alerts", "cameras" })
        {
            var now = clock.GetUtcNow();
            FeedStatus status; lock (_sync) status = _feeds[id];
            if (status.NextAttempt > now) continue;
            if (!traffic.Configured)
            {
                lock (_sync) _feeds[id] = status with { State = "unconfigured", LastAttempt = now,
                    NextAttempt = now.AddSeconds(120), Message = "Set ROAD_TRAFFIC_API_BASE_URL and ROAD_TRAFFIC_API_KEY on the server to enable road traffic." };
                continue;
            }
            try
            {
                var result = await traffic.FetchAsync(id, ct);
                lock (_sync)
                {
                    _traffic[id] = result;
                    _feeds[id] = status with { State = "ready", LastSuccess = clock.GetUtcNow(),
                        LastAttempt = now, NextAttempt = clock.GetUtcNow().AddSeconds(120), Message = "Live feed available." };
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException or OperationCanceledException or InvalidOperationException)
            { Failure(id, status, now, 120); }
        }
    }
    private async Task PollMarineAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow(); FeedStatus status; lock (_sync) status = _feeds["marine"];
        if (status.NextAttempt > now) return;
        var bounds = settings.Current?.MarineBounds;
        if (bounds is null) return;
        try
        {
            var result = await marine.FetchAsync(bounds, ct);
            lock (_sync)
            {
                _marine = result; _marineBounds = bounds;
                _feeds["marine"] = status with { State = "ready", LastSuccess = clock.GetUtcNow(), LastAttempt = now,
                    NextAttempt = clock.GetUtcNow().AddSeconds(60), Message = result.Vessels.Length == 0
                        ? "No AIS reports in this area. Local receiver coverage cannot be confirmed; this does not mean there are no vessels."
                        : result.Truncated ? "AIS response was truncated; some vessels may be missing." : "AIS available; receiver coverage may be incomplete." };
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException or OperationCanceledException)
        { Failure("marine", status, now, 60); }
    }
    private void Failure(string id, FeedStatus status, DateTimeOffset now, int delay)
    {
        lock (_sync) _feeds[id] = status with { State = status.LastSuccess.HasValue ? "stale" : "unavailable", LastAttempt = now,
            NextAttempt = clock.GetUtcNow().AddSeconds(delay), Message = status.LastSuccess.HasValue
                ? "Provider unavailable. Showing last successful data; it may be out of date. Automatic retry scheduled."
                : "Provider unavailable or returned an invalid response. Automatic retry scheduled." };
    }
    public CommuteSnapshot Snapshot(DateTimeOffset? at = null)
    {
        var now = at ?? clock.GetUtcNow(); var route = settings.Current;
        var workspace = settings.Workspace;
        var profile = workspace.Dashboards.FirstOrDefault(p => p.Id == workspace.ActiveDashboardId);
        if (route is null)
            return new(null, "UNKNOWN", settings.Available ? "Save a route in Build my dashboard to begin monitoring." : "Database settings unavailable. Saved routes cannot be loaded.",
                [], [], [], [], [], null, now, [], profile, settings.Available);
        lock (_sync)
        {
            var feeds = _feeds.Values.Select(f => f.State == "ready" && now - f.LastSuccess > TimeSpan.FromMinutes(f.Id == "marine" ? 3 : 5)
                ? f with { State = "stale", Message = "Refresh is overdue. Cached data may be out of date." } : f).ToArray();
            if (route.MarineBounds is null)
                feeds = feeds.Select(f => f.Id == "marine" ? f with { State = "unconfigured", Message = "Marine monitoring is disabled for this route." } : f).ToArray();
            else if (_marineBounds != route.MarineBounds)
                feeds = feeds.Select(f => f.Id == "marine" ? f with {
                    State = f.State is "stale" or "unavailable" ? "unavailable" : "waiting", LastSuccess = null,
                    Message = f.State is "stale" or "unavailable" ? "Provider unavailable for the configured marine area. Automatic retry scheduled."
                        : "Marine area changed; waiting for next scheduled refresh." } : f).ToArray();
            var cameras = _traffic.Values.SelectMany(x => x.Cameras)
                .Where(c => RoadMatches(c.Roadway, route) && RouteGeometry.Project(c.Position, route.Points).Distance <= route.CorridorKm)
                .OrderBy(c => RouteGeometry.Project(c.Position, route.Points).Along).ToArray();
            var items = _traffic.Values.SelectMany(x => x.Items).Where(item => Relevant(item, route, now))
                .DistinctBy(i => i.Id)
                .OrderByDescending(i => i.FullClosure).ThenBy(i => i.Kind == "incident" ? 0 : i.Kind == "alert" ? 1 : 2)
                .ThenBy(i => i.StartsAt > now).ToArray();
            var b = route.MarineBounds;
            var vessels = b is not null && _marineBounds == b ? _marine.Vessels.Where(v => v.Position.Latitude >= b.MinLatitude && v.Position.Latitude <= b.MaxLatitude
                && v.Position.Longitude >= b.MinLongitude && v.Position.Longitude <= b.MaxLongitude)
                .OrderBy(v => route.Bridge is { } bridge ? RouteGeometry.Distance(v.Position, bridge.Position) : 0).ToArray() : [];
            var marineFresh = feeds.Single(f => f.Id == "marine").State == "ready";
            var predictions = marineFresh && route.Bridge is { } configuredBridge ? vessels.Select(v => predictor.Predict(v, configuredBridge, now))
                .OrderByDescending(p => p.Level switch { "IMMINENT" => 3, "LIKELY" => 2, "POSSIBLE" => 1, _ => 0 })
                .ThenBy(p => p.WindowStart).ToArray() : [];
            var active = items.Where(i => i.StartsAt is null || i.StartsAt <= now).ToArray();
            var healthy = feeds.Where(f => f.Id is "incidents" or "construction" or "conditions" or "alerts").All(f => f.State == "ready");
            var status = active.Any(i => i.FullClosure) ? "DISRUPTED" : active.Any(i => i.Advisory || i.Kind == "alert") ? "CAUTION" : healthy ? "NO REPORTED ISSUES" : "UNKNOWN";
            var summary = status switch
            {
                "DISRUPTED" => "A full closure is reported along this corridor. Check the event schedule before travel.",
                "CAUTION" => "Incidents, restrictions or regional advisories are reported for this corridor. Review details and schedules.",
                "NO REPORTED ISSUES" => "No active issues reported by the road provider. Traffic speed and travel time are not measured in Phase 1.",
                _ => "Road data is incomplete. Current commute conditions cannot be confirmed."
            };
            if (!healthy && status != "UNKNOWN") summary += " Some road feeds are unavailable or stale.";
            return new(route, status, summary, cameras, items, vessels, predictions, feeds, route.Bridge?.Position, now, _marine.Attribution, profile);
        }
    }
    public static bool RoadMatches(string roadway, CommuteRoute route) => route.Roadways.Any(alias =>
        Regex.IsMatch(roadway, @"(?<![\p{L}\p{N}])" + Regex.Escape(alias.Trim()) + @"(?![\p{L}\p{N}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
    public static bool Relevant(RoadItem item, CommuteRoute route, DateTimeOffset now)
    {
        if (item.EndsAt < now || item.StartsAt > now.AddDays(7)) return false;
        if (item.Regional) return item.Roadway.Split('|').Any(r => route.AlertRegions.Contains(r, StringComparer.OrdinalIgnoreCase));
        if (!RoadMatches(item.Roadway, route) || !RouteGeometry.Relevant(item.Geometry, route)) return false;
        return route.Direction == "All Directions" || item.Direction is "" or "Unknown" or "Both Directions" or "All Directions"
            || item.Direction.Equals(route.Direction, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class CommutePollingWorker(CommuteService service) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do { await service.PollAsync(stoppingToken); } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
