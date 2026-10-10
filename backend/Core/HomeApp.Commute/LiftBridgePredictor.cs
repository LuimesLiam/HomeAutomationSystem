namespace HomeApp.Commute;

public interface ILiftBridgePredictor { LiftPrediction Predict(Vessel vessel, BridgeConfiguration bridge, DateTimeOffset now); }
public sealed class LiftBridgePredictor : ILiftBridgePredictor
{
    public LiftPrediction Predict(Vessel v, BridgeConfiguration bridge, DateTimeOffset now)
    {
        var distance = RouteGeometry.Distance(v.Position, bridge.Position);
        // The canal's inbound axis comes from the saved bridge configuration.
        var harbourSide = RouteGeometry.Angle(RouteGeometry.Bearing(v.Position, bridge.Position), bridge.InboundBearing) > 90;
        var direction = harbourSide ? "Outbound" : "Inbound";
        LiftPrediction Low(string explanation) => new("LOW", v.Mmsi, v.Name, "Undetermined", null, null, 0, explanation);
        if (!v.PositionFreshnessKnown) return Low("Last AIS update was static vessel data or an unknown message type; position freshness cannot be confirmed.");
        if (v.SeenAt is null || v.SeenAt > now.AddMinutes(1) || now - v.SeenAt > TimeSpan.FromMinutes(5))
            return Low("AIS position is missing or older than five minutes; no reliable approach estimate.");
        if (v.SpeedKnots is null || v.SpeedKnots < .5 || v.NavigationStatus is 1 or 5 or 6)
            return Low("Vessel is stationary, berthed, or has no usable speed; no approach detected.");
        if (v.Course is null) return Low("Course over ground is unavailable; heading alone cannot establish an approach.");
        if (distance > 25) return Low("Vessel is outside the 25 km bridge approach area.");
        var angle = RouteGeometry.Angle(v.Course.Value, RouteGeometry.Bearing(v.Position, bridge.Position));
        var crossTrack = distance * Math.Sin(angle * Math.PI / 180);
        if (angle > 35 || crossTrack > .65) return Low("Current trajectory does not intersect the canal approach; vessel may be passing nearby or moving away.");
        var elapsedHours = Math.Max(0, (now - v.SeenAt.Value).TotalHours);
        var remaining = distance * Math.Cos(angle * Math.PI / 180) - v.SpeedKnots.Value * 1.852 * elapsedHours;
        if (remaining < -.25) return Low("Reported trajectory would already have passed the bridge; awaiting a newer AIS position.");
        var minutes = Math.Max(0, remaining) / (v.SpeedKnots.Value * 1.852) * 60;
        if (minutes > 90) return Low("At current speed the vessel is more than 90 minutes from the canal.");
        // AIS does not provide air draught. Size/type are proxies, never proof that a lift is necessary.
        var commercial = v.TypeCode is >= 60 and <= 89 || v.TypeCode is 31 or 32 or 52 || v.LengthMetres >= 40;
        var likely = commercial && angle <= 20 && crossTrack <= .35;
        var level = likely ? minutes <= 8 ? "IMMINENT" : "LIKELY" : "POSSIBLE";
        var ageMinutes = (now - v.SeenAt.Value).TotalMinutes;
        var confidence = (int)Math.Clamp((likely ? 78 : 45) - ageMinutes * 5 - angle * .4
            - (v.LengthMetres is null ? 8 : 0) - (v.Heading is { } h && RouteGeometry.Angle(h, v.Course.Value) > 40 ? 10 : 0), 15, 85);
        var uncertainty = Math.Max(4, minutes * .3 + ageMinutes);
        return new(level, v.Mmsi, v.Name, direction, now.AddMinutes(Math.Max(0, minutes - uncertainty - 3)),
            now.AddMinutes(minutes + uncertainty), confidence,
            $"{v.Type} vessel {direction.ToLowerInvariant()} toward {bridge.Name}, {distance:F1} km away at {v.SpeedKnots:F1} kn. " +
            "Window allows for approach slowdown and opening lead time. Air draught, pilot intentions and bridge schedule are unknown; confidence is a heuristic score, not a calibrated probability.");
    }
}
