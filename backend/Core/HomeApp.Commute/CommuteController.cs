using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeApp.Commute;
[ApiController]
[Route("api/commute")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CommuteController(CommuteService service, RouteSettingsStore settings) : ControllerBase
{
    [HttpGet] public ActionResult<CommuteSnapshot> Get() => service.Snapshot();
    [HttpGet("workspace")]
    public async Task<ActionResult<WorkspaceDto>> GetWorkspace(CancellationToken ct)
    {
        await settings.InitializeAsync(ct);
        return settings.Available ? settings.PublicWorkspace() : Problem("Database settings are unavailable.", statusCode: 503);
    }
    [HttpGet("route")] public ActionResult<CommuteRoute> GetRoute() => settings.Current is { } route ? route : NotFound();
    [HttpPut("route")]
    public async Task<ActionResult<CommuteRoute>> SaveRoute(CommuteRoute route, CancellationToken ct)
    {
        if (RouteSettingsStore.Validate(route) is { } error) return BadRequest(new { message = error });
        try { return await settings.SaveAsync(route, ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "Settings changed in another session. Reload and try again." }); }
        catch (Exception e) when (e is DbUpdateException or InvalidOperationException or System.Data.Common.DbException)
        { return Problem("Unable to save database settings. Check the database connection.", statusCode: 503); }
    }
    [HttpPut("route/reverse")]
    public Task<ActionResult<WorkspaceDto>> ReverseRoute(ReverseRouteRequest request, CancellationToken ct) => WriteAsync(w => {
        var route = w.Routes.FirstOrDefault(r => r.Id == request.RouteId && r.Id == w.ActiveRouteId)
            ?? throw new ArgumentException("The active route changed. Refresh before reversing your trip.");
        var direction = route.Direction switch {
            "Eastbound" => "Westbound", "Westbound" => "Eastbound",
            "Northbound" => "Southbound", "Southbound" => "Northbound",
            "Inbound" => "Outbound", "Outbound" => "Inbound", _ => "All Directions"
        };
        var reversed = route with { Origin = route.Destination, Destination = route.Origin,
            Name = $"{route.Destination} → {route.Origin}", Direction = direction, Points = route.Points.Reverse().ToArray() };
        if (reversed.Name.Length > 120) reversed = reversed with { Name = route.Name };
        return w with { Routes = w.Routes.Select(r => r.Id == reversed.Id ? reversed : r).ToArray() };
    }, ct);
    [HttpPut("dashboards")]
    public async Task<ActionResult<WorkspaceDto>> SaveDashboard(DashboardProfile profile, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Length > 120 || profile.RouteIds is null
            || profile.Panels is null || profile.Panels.Length > 5 || profile.Panels.Distinct().Count() != profile.Panels.Length
            || profile.Panels.Any(p => p is not ("map" or "bridge" or "reports" or "cameras" or "marine"))
            || profile.FavoriteViews is null || profile.FavoriteViews.Length > 100
            || profile.FavoriteViews.Any(v => v is null || string.IsNullOrWhiteSpace(v.CameraId) || string.IsNullOrWhiteSpace(v.ViewId)
                || v.CameraId.Length > 120 || v.ViewId.Length > 120))
            return BadRequest(new { message = "Provide a name, valid panels and up to 100 favorite camera views." });
        var saved = profile with { Id = profile.Id == Guid.Empty ? Guid.NewGuid() : profile.Id,
            RouteIds = profile.RouteIds.Distinct().ToArray(), FavoriteViews = profile.FavoriteViews.Distinct().ToArray() };
        return await WriteAsync(w => {
            if (saved.RouteIds.Any(id => !w.Routes.Any(r => r.Id == id))) throw new ArgumentException("A selected route no longer exists.");
            var activeRoute = saved.RouteIds.Contains(w.ActiveRouteId ?? Guid.Empty) ? w.ActiveRouteId : saved.RouteIds.Select(id => (Guid?)id).FirstOrDefault();
            return w with { Dashboards = w.Dashboards.Where(p => p.Id != saved.Id).Append(saved).ToArray(),
                ActiveDashboardId = saved.Id, ActiveRouteId = activeRoute };
        }, ct);
    }
    [HttpPut("active")]
    public Task<ActionResult<WorkspaceDto>> Activate(ActivateWorkspaceRequest request, CancellationToken ct) => WriteAsync(w => {
        if (!w.Routes.Any(r => r.Id == request.RouteId)) throw new ArgumentException("Route not found.");
        var dashboard = request.DashboardId.HasValue ? w.Dashboards.FirstOrDefault(p => p.Id == request.DashboardId) : null;
        if (request.DashboardId.HasValue && (dashboard is null || !dashboard.RouteIds.Contains(request.RouteId)))
            throw new ArgumentException("The route is not included in that dashboard.");
        return w with { ActiveRouteId = request.RouteId, ActiveDashboardId = request.DashboardId };
    }, ct);
    [HttpDelete("routes/{id:guid}")]
    public Task<ActionResult<WorkspaceDto>> DeleteRoute(Guid id, CancellationToken ct) => WriteAsync(w => {
        var routes = w.Routes.Where(r => r.Id != id).ToArray();
        var profiles = w.Dashboards.Select(p => p with { RouteIds = p.RouteIds.Where(r => r != id).ToArray() }).ToArray();
        return w with { Routes = routes, Dashboards = profiles, ActiveRouteId = w.ActiveRouteId == id ? routes.Select(r => (Guid?)r.Id).FirstOrDefault() : w.ActiveRouteId,
            ActiveDashboardId = null };
    }, ct);
    [HttpDelete("dashboards/{id:guid}")]
    public Task<ActionResult<WorkspaceDto>> DeleteDashboard(Guid id, CancellationToken ct) => WriteAsync(w => w with {
        Dashboards = w.Dashboards.Where(p => p.Id != id).ToArray(), ActiveDashboardId = w.ActiveDashboardId == id ? null : w.ActiveDashboardId
    }, ct);
    [HttpPut("maps/key")]
    public Task<ActionResult<WorkspaceDto>> SaveMapsKey(GoogleMapsKeyRequest request, CancellationToken ct)
    {
        if (request.Key is null || request.Key.Length > 200 || request.Key.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
            return Task.FromResult<ActionResult<WorkspaceDto>>(BadRequest(new { message = "Enter a valid Google Maps Embed API key, or leave it empty to remove it." }));
        return WriteAsync(w => w with { GoogleMapsEmbedKey = request.Key }, ct);
    }
    [HttpGet("maps")]
    public ActionResult<RouteMaps> Maps([FromQuery] Guid? routeId)
    {
        var w = settings.Workspace;
        var route = w.Routes.FirstOrDefault(r => r.Id == (routeId ?? w.ActiveRouteId));
        if (route is null) return NotFound();
        static string Coordinate(GeoPoint p) => FormattableString.Invariant($"{p.Latitude},{p.Longitude}");
        static string Encode(string s) => Uri.EscapeDataString(s);
        var origin = Encode(Coordinate(route.Points[0])); var destination = Encode(Coordinate(route.Points[^1]));
        // Mobile Maps URLs support three intermediate stops; Embed supports twenty.
        static GeoPoint[] Stops(GeoPoint[] points, int maximum)
        {
            var intermediate = points.Skip(1).SkipLast(1).ToArray();
            return intermediate.Length <= maximum ? intermediate : Enumerable.Range(0, maximum)
                .Select(i => intermediate[(int)Math.Round(i * (intermediate.Length - 1d) / (maximum - 1))]).ToArray();
        }
        var urlStops = Stops(route.Points, 3); var embedStops = Stops(route.Points, 20);
        var directions = $"https://www.google.com/maps/dir/?api=1&origin={origin}&destination={destination}&travelmode=driving";
        if (urlStops.Length > 0) directions += "&waypoints=" + Encode(string.Join("|", urlStops.Select(Coordinate)));
        string? embed = null;
        if (w.GoogleMapsEmbedKey.Length > 0)
        {
            embed = $"https://www.google.com/maps/embed/v1/directions?key={Encode(w.GoogleMapsEmbedKey)}&origin={origin}&destination={destination}&mode=driving";
            if (embedStops.Length > 0) embed += "&waypoints=" + Encode(string.Join("|", embedStops.Select(Coordinate)));
        }
        return new RouteMaps(directions, embed);
    }
    private async Task<ActionResult<WorkspaceDto>> WriteAsync(Func<CommuteWorkspace, CommuteWorkspace> change, CancellationToken ct)
    {
        try { await settings.ChangeAsync(change, ct); return settings.PublicWorkspace(); }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "Settings changed in another session. Reload and try again." }); }
        catch (Exception e) when (e is DbUpdateException or InvalidOperationException or System.Data.Common.DbException)
        { return Problem("Unable to save database settings. Check the database connection.", statusCode: 503); }
    }
}
