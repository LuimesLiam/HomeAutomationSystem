namespace HomeApp.Commute;

public static class RouteGeometry
{
    public static double Distance(GeoPoint a, GeoPoint b)
    {
        var lat = (a.Latitude + b.Latitude) * Math.PI / 360;
        return Math.Sqrt(Math.Pow((a.Longitude - b.Longitude) * 111.32 * Math.Cos(lat), 2)
            + Math.Pow((a.Latitude - b.Latitude) * 111.32, 2));
    }

    public static (double Distance, double Along) Project(GeoPoint p, IReadOnlyList<GeoPoint> route)
    {
        double best = double.MaxValue, along = 0, travelled = 0;
        for (int i = 1; i < route.Count; i++)
        {
            var a = route[i - 1]; var b = route[i];
            var cos = Math.Cos(p.Latitude * Math.PI / 180);
            var dx = (b.Longitude - a.Longitude) * 111.32 * cos;
            var dy = (b.Latitude - a.Latitude) * 111.32;
            var px = (p.Longitude - a.Longitude) * 111.32 * cos;
            var py = (p.Latitude - a.Latitude) * 111.32;
            var squared = dx * dx + dy * dy;
            var t = squared == 0 ? 0 : Math.Clamp((px * dx + py * dy) / squared, 0, 1);
            var distance = Math.Sqrt(Math.Pow(px - t * dx, 2) + Math.Pow(py - t * dy, 2));
            if (distance < best) { best = distance; along = travelled + t * Math.Sqrt(squared); }
            travelled += Math.Sqrt(squared);
        }
        return (best, along);
    }

    public static bool Relevant(GeoPoint[] geometry, CommuteRoute route)
    {
        if (geometry.Any(p => Project(p, route.Points).Distance <= route.CorridorKm)) return true;
        // A long reported segment can cross the corridor with both endpoints outside it.
        if (geometry.Length > 1 && route.Points.Any(p => Project(p, geometry).Distance <= route.CorridorKm)) return true;
        for (var i = 1; i < geometry.Length; i++)
            for (var j = 1; j < route.Points.Length; j++)
                if (Crosses(geometry[i - 1], geometry[i], route.Points[j - 1], route.Points[j])) return true;
        return false;
    }
    private static bool Crosses(GeoPoint a, GeoPoint b, GeoPoint c, GeoPoint d)
    {
        static double Side(GeoPoint p, GeoPoint q, GeoPoint r) =>
            (q.Longitude - p.Longitude) * (r.Latitude - p.Latitude) - (q.Latitude - p.Latitude) * (r.Longitude - p.Longitude);
        return Side(a, b, c) * Side(a, b, d) < 0 && Side(c, d, a) * Side(c, d, b) < 0;
    }
    public static double Bearing(GeoPoint a, GeoPoint b) =>
        (Math.Atan2((b.Longitude - a.Longitude) * Math.Cos(a.Latitude * Math.PI / 180), b.Latitude - a.Latitude)
        * 180 / Math.PI + 360) % 360;
    public static double Angle(double a, double b) => Math.Abs((a - b + 540) % 360 - 180);
    public static bool Valid(GeoPoint p) => double.IsFinite(p.Latitude) && double.IsFinite(p.Longitude)
        && p.Latitude is >= -90 and <= 90 && p.Longitude is >= -180 and <= 180;

    public static GeoPoint[] Decode(string encoded)
    {
        var points = new List<GeoPoint>(); int index = 0, lat = 0, lon = 0;
        int Read()
        {
            int result = 0, shift = 0, b;
            do
            {
                if (index >= encoded.Length || shift > 30) throw new FormatException("Invalid polyline");
                b = encoded[index++] - 63;
                if (b is < 0 or > 63) throw new FormatException("Invalid polyline");
                result |= (b & 31) << shift; shift += 5;
            } while (b >= 32);
            return (result & 1) != 0 ? ~(result >> 1) : result >> 1;
        }
        try
        {
            while (index < encoded.Length)
            {
                lat += Read(); lon += Read();
                var p = new GeoPoint(lat / 1e5, lon / 1e5);
                if (!Valid(p)) return [];
                points.Add(p);
            }
            return points.ToArray();
        }
        catch (FormatException) { return []; }
    }
}
