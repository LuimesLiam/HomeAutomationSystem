using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace HomeApp.Commute;

public interface IRoadTrafficProvider { bool Configured { get; } Task<TrafficFeed> FetchAsync(string resource, CancellationToken ct); }
public interface IMarineTrafficProvider { Task<MarineFeed> FetchAsync(MarineBounds bounds, CancellationToken ct); }

internal static class FeedJson
{
    public static string Text(JsonElement e, string name) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var p) && p.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined) ? p.ToString() : "";
    public static double? Number(JsonElement e, string name) => double.TryParse(Text(e, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : null;
    public static DateTimeOffset? Time(JsonElement e, string name)
    {
        var s = Text(e, name);
        if (long.TryParse(s, out var unix) && unix > 0 && unix < 253402300800) return DateTimeOffset.FromUnixTimeSeconds(unix);
        return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t) ? t : null;
    }
    public static string[] Strings(JsonElement e, string name) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Array
        ? p.EnumerateArray().Select(x => x.ToString()).ToArray() : Text(e, name) is { Length: > 0 } s ? [s] : [];
    public static GeoPoint? Point(JsonElement e, string lat = "Latitude", string lon = "Longitude") =>
        Number(e, lat) is { } a && Number(e, lon) is { } b && RouteGeometry.Valid(new(a, b)) ? new(a, b) : null;
    public static GeoPoint[] Geometry(JsonElement e)
    {
        var poly = Strings(e, "EncodedPolyline").SelectMany(RouteGeometry.Decode).ToArray();
        if (poly.Length > 0) return poly;
        var a = Point(e); var b = Point(e, "LatitudeSecondary", "LongitudeSecondary");
        return a is null ? [] : b is null ? [a] : [a, b];
    }
}

public sealed class RoadTrafficProvider(HttpClient http, IConfiguration config) : IRoadTrafficProvider
{
    private string? ApiKey => config["ROAD_TRAFFIC_API_KEY"] ?? config["Commute:RoadTrafficApiKey"];
    private Uri? ApiBase => Uri.TryCreate(config["ROAD_TRAFFIC_API_BASE_URL"] ?? config["Commute:RoadTrafficApiBaseUrl"], UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
        ? uri : null;
    public bool Configured => !string.IsNullOrWhiteSpace(ApiKey) && ApiBase is not null;
    public async Task<TrafficFeed> FetchAsync(string resource, CancellationToken ct)
    {
        var key = ApiKey;
        var apiBase = ApiBase;
        if (!Configured || apiBase is null) throw new InvalidOperationException("Road traffic API endpoint and key are not configured.");
        var path = resource switch
        {
            "cameras" => "v2/get/cameras", "incidents" => "v2/get/event", "construction" => "v2/get/constructionprojects",
            "conditions" => "v3/get/roadconditions", "alerts" => "v2/get/alerts", _ => throw new ArgumentException("Unknown feed")
        };
        using var response = await http.GetAsync($"{apiBase.AbsoluteUri.TrimEnd('/')}/{path}?key={Uri.EscapeDataString(key!)}&format=json&lang=en", ct);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return Parse(resource, doc.RootElement);
    }
    public static TrafficFeed Parse(string resource, JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array) throw new JsonException("Expected a road traffic array.");
        var cameras = new List<TrafficCamera>(); var items = new List<RoadItem>();
        foreach (var e in root.EnumerateArray())
        {
            if (e.ValueKind != JsonValueKind.Object) continue;
            var id = FeedJson.Text(e, "ID"); if (id.Length == 0) id = FeedJson.Text(e, "Id");
            if (resource == "cameras")
            {
                var position = FeedJson.Point(e); if (position is null) continue;
                var views = new List<CameraView>();
                if (e.TryGetProperty("Views", out var v) && v.ValueKind == JsonValueKind.Array)
                    foreach (var view in v.EnumerateArray())
                    {
                        var url = FeedJson.Text(view, "Url");
                        // Render provider-supplied HTTPS images directly; never proxy arbitrary URLs.
                        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https")
                            views.Add(new(FeedJson.Text(view, "Id"), url, FeedJson.Text(view, "Description"),
                                FeedJson.Text(view, "Status").Equals("Enabled", StringComparison.OrdinalIgnoreCase)));
                    }
                cameras.Add(new(id, FeedJson.Text(e, "Location"), FeedJson.Text(e, "Roadway"), FeedJson.Text(e, "Direction"), position, views.ToArray()));
                continue;
            }
            var geometry = FeedJson.Geometry(e);
            if (resource == "alerts")
            {
                // Alerts have regions, not precise coordinates. Encode regions for route matching.
                var regions = FeedJson.Strings(e, "Regions");
                items.Add(new($"alerts:{id}", "alert", "Road traffic regional alert", FeedJson.Text(e, "Message") + " " + FeedJson.Text(e, "Notes"),
                    string.Join("|", regions), "", null, [], FeedJson.Time(e, "LastUpdated"), FeedJson.Time(e, "StartTime"), FeedJson.Time(e, "EndTime"),
                    false, "", "", true, FeedJson.Text(e, "HighImportance").Equals("True", StringComparison.OrdinalIgnoreCase)));
            }
            else if (resource == "conditions")
            {
                var conditions = FeedJson.Strings(e, "Condition");
                var visibility = FeedJson.Text(e, "Visibility"); var drifting = FeedJson.Text(e, "Drifting");
                bool advisory = conditions.Any(c => !c.Contains("bare and dry", StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrEmpty(visibility) && !visibility.Equals("Good", StringComparison.OrdinalIgnoreCase))
                    || drifting.Equals("Yes", StringComparison.OrdinalIgnoreCase);
                items.Add(new($"conditions:{items.Count}", "condition", FeedJson.Text(e, "LocationDescription"),
                    $"{string.Join(", ", conditions)}. Visibility: {visibility}. Drifting: {drifting}", FeedJson.Text(e, "RoadwayName"), "",
                    geometry.FirstOrDefault(), geometry, FeedJson.Time(e, "LastUpdated"), null, null, false, "", "", false, advisory));
            }
            else
            {
                var kind = resource == "construction" || FeedJson.Text(e, "EventType") == "roadwork" ? "construction" : "incident";
                items.Add(new($"{resource}:{id}", kind, FeedJson.Text(e, "EventSubType") is { Length: > 0 } sub ? sub : kind == "construction" ? "Construction / lane restriction" : "Traffic incident",
                    FeedJson.Text(e, "Description") + " " + FeedJson.Text(e, "Comment"), FeedJson.Text(e, "RoadwayName"), FeedJson.Text(e, "DirectionOfTravel"),
                    geometry.FirstOrDefault(), geometry, FeedJson.Time(e, "LastUpdated"), FeedJson.Time(e, "StartDate"), FeedJson.Time(e, "PlannedEndDate"),
                    FeedJson.Text(e, "IsFullClosure").Equals("True", StringComparison.OrdinalIgnoreCase), FeedJson.Text(e, "LanesAffected"),
                    FeedJson.Text(e, "Recurrence") + " " + FeedJson.Text(e, "RecurrenceSchedules"), false, true));
            }
        }
        return new(cameras.ToArray(), items.ToArray());
    }
}

public sealed class OpenWatersProvider(HttpClient http) : IMarineTrafficProvider
{
    public async Task<MarineFeed> FetchAsync(MarineBounds b, CancellationToken ct)
    {
        var bounds = FormattableString.Invariant($"{b.MinLatitude},{b.MinLongitude},{b.MaxLatitude},{b.MaxLongitude}");
        using var response = await http.GetAsync($"https://ais.openwaters.io/v1/vessels?bbox={bounds}&kind=vessel&max_age=30m&max_age_moving=30m", ct);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return Parse(doc.RootElement);
    }
    public static MarineFeed Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array) throw new JsonException("Expected AIS GeoJSON.");
        var vessels = new List<Vessel>();
        foreach (var f in features.EnumerateArray())
        {
            if (f.ValueKind != JsonValueKind.Object || !f.TryGetProperty("geometry", out var g) || g.ValueKind != JsonValueKind.Object
                || !g.TryGetProperty("coordinates", out var c) || c.ValueKind != JsonValueKind.Array || c.GetArrayLength() < 2
                || c[0].ValueKind != JsonValueKind.Number || c[1].ValueKind != JsonValueKind.Number || !c[0].TryGetDouble(out var lon) || !c[1].TryGetDouble(out var lat) || !RouteGeometry.Valid(new(lat, lon))
                || !f.TryGetProperty("properties", out var p) || p.ValueKind != JsonValueKind.Object) continue;
            if (FeedJson.Text(p, "kind") is { Length: > 0 } kind && kind != "vessel") continue;
            var mmsi = FeedJson.Text(p, "mmsi"); if (mmsi.Length == 0) mmsi = FeedJson.Text(f, "id");
            var type = (int?)FeedJson.Number(p, "type");
            var course = FeedJson.Number(p, "cog"); var heading = FeedJson.Number(p, "heading"); var speed = FeedJson.Number(p, "sog");
            vessels.Add(new(mmsi, FeedJson.Text(p, "name") is { Length: > 0 } name ? name : $"MMSI {mmsi}", new(lat, lon),
                course is >= 0 and < 360 ? course : null, heading is >= 0 and < 360 ? heading : null,
                speed is >= 0 and < 102.3 ? speed : null, type, TypeName(type), Positive(p, "length"), Positive(p, "beam"),
                FeedJson.Text(p, "destination"), FeedJson.Time(p, "seen"), FeedJson.Text(p, "source"), (int?)FeedJson.Number(p, "nav_status"),
                FeedJson.Text(p, "msg_type") is "PositionReport" or "StandardClassBPositionReport" or "ExtendedClassBPositionReport" or "LongRangeAisBroadcastMessage"));
        }
        var attribution = root.TryGetProperty("attribution", out var a) && a.ValueKind == JsonValueKind.Object
            ? a.EnumerateObject().Select(x => x.Value.ToString()).ToArray() : ["Open Waters AIS (https://openwaters.io/ais/)"];
        return new(vessels.ToArray(), attribution, FeedJson.Text(root, "truncated").Equals("True", StringComparison.OrdinalIgnoreCase));
    }
    private static double? Positive(JsonElement p, string key) => FeedJson.Number(p, key) is > 0 and var value ? value : null;
    public static string TypeName(int? code) => code switch
    {
        >= 70 and <= 79 => "Cargo", >= 80 and <= 89 => "Tanker", >= 60 and <= 69 => "Passenger",
        30 => "Fishing", 31 or 32 or 52 => "Tug / towing", 36 => "Sailing", 37 => "Pleasure craft",
        >= 40 and <= 49 => "High-speed craft", 51 => "Search and rescue", 50 => "Pilot", 53 => "Port tender",
        >= 90 and <= 99 => "Other vessel", _ => "Unknown"
    };
}
