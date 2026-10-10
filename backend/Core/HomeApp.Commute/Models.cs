namespace HomeApp.Commute;

public sealed record GeoPoint(double Latitude, double Longitude);
public sealed record MarineBounds(double MinLatitude, double MinLongitude, double MaxLatitude, double MaxLongitude);
public sealed record BridgeConfiguration(string Name, GeoPoint Position, double InboundBearing);
public sealed record CommuteRoute
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string Origin { get; init; } = "";
    public string Destination { get; init; } = "";
    public string[] Roadways { get; init; } = [];
    public string[] AlertRegions { get; init; } = [];
    public string Direction { get; init; } = "All Directions";
    public double CorridorKm { get; init; } = 2;
    public GeoPoint[] Points { get; init; } = [];
    public MarineBounds? MarineBounds { get; init; }
    public BridgeConfiguration? Bridge { get; init; }
}
public sealed record FavoriteCameraView(string CameraId, string ViewId);
public sealed record DashboardProfile
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public Guid[] RouteIds { get; init; } = [];
    public FavoriteCameraView[] FavoriteViews { get; init; } = [];
    public string[] Panels { get; init; } = ["map", "bridge", "reports", "cameras", "marine"];
    public bool FavoritesOnly { get; init; }
}
public sealed record CommuteWorkspace
{
    public CommuteRoute[] Routes { get; init; } = [];
    public DashboardProfile[] Dashboards { get; init; } = [];
    public Guid? ActiveRouteId { get; init; }
    public Guid? ActiveDashboardId { get; init; }
    public string GoogleMapsEmbedKey { get; init; } = "";
}
public sealed record WorkspaceDto(CommuteRoute[] Routes, DashboardProfile[] Dashboards, Guid? ActiveRouteId,
    Guid? ActiveDashboardId, bool GoogleMapsConfigured);
public sealed record ActivateWorkspaceRequest(Guid RouteId, Guid? DashboardId);
public sealed record GoogleMapsKeyRequest(string Key);
public sealed record RouteMaps(string DirectionsUrl, string? EmbedUrl);
public sealed record RoadItem(string Id, string Kind, string Title, string Description, string Roadway,
    string Direction, GeoPoint? Position, GeoPoint[] Geometry, DateTimeOffset? UpdatedAt,
    DateTimeOffset? StartsAt, DateTimeOffset? EndsAt, bool FullClosure, string Lanes, string Schedule,
    bool Regional = false, bool Advisory = false);
public sealed record CameraView(string Id, string Url, string Description, bool Enabled);
public sealed record TrafficCamera(string Id, string Name, string Roadway, string Direction,
    GeoPoint Position, CameraView[] Views);
public sealed record Vessel(string Mmsi, string Name, GeoPoint Position, double? Course, double? Heading,
    double? SpeedKnots, int? TypeCode, string Type, double? LengthMetres, double? BeamMetres,
    string Destination, DateTimeOffset? SeenAt, string Source, int? NavigationStatus, bool PositionFreshnessKnown = false);
public sealed record LiftPrediction(string Level, string VesselMmsi, string VesselName, string Direction,
    DateTimeOffset? WindowStart, DateTimeOffset? WindowEnd, int Confidence, string Explanation);
public sealed record FeedStatus(string Id, string Name, string State, DateTimeOffset? LastSuccess,
    DateTimeOffset? LastAttempt, DateTimeOffset? NextAttempt, string Message);
public sealed record CommuteSnapshot(CommuteRoute? Route, string Status, string Summary,
    TrafficCamera[] Cameras, RoadItem[] Items, Vessel[] Vessels, LiftPrediction[] Predictions,
    FeedStatus[] Feeds, GeoPoint? Bridge, DateTimeOffset GeneratedAt, string[] Attribution,
    DashboardProfile? Dashboard = null, bool SettingsAvailable = true);
public sealed record TrafficFeed(TrafficCamera[] Cameras, RoadItem[] Items);
public sealed record MarineFeed(Vessel[] Vessels, string[] Attribution, bool Truncated);

public sealed record ReverseRouteRequest(Guid RouteId);
