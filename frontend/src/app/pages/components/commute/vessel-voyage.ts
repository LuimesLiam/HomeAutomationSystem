import type { CommuteSnapshot, GeoPoint, Vessel } from "./commute.models";

export interface DestinationPort {
  code: string;
  name: string;
  country: string;
  position: GeoPoint;
}
export interface VesselVoyage {
  destination: string;
  port: DestinationPort | null;
  connection: { points: GeoPoint[] } | null;
  unavailableReason: string;
}

export function reportedDestination(vessel: Vessel | null): string {
  const destination = (vessel?.destination ?? "").replace(/@+$/g, "").trim();
  return !destination ||
    /^(unknown|n\/a|none|not available|not reported|\?+)$/i.test(destination)
    ? "Not reported by AIS"
    : destination;
}

function normalize(value: string): string {
  return value
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toUpperCase()
    .replace(/[^A-Z0-9]/g, "");
}

// Resolve exact codes/names only. An ambiguous name must never select a guessed port.
const portIndex = new Map<string, DestinationPort | null>();
export type PortRow = readonly [string, string, string, number, number, string];

// Replace the lookup with deployment data. An empty catalogue never guesses a port.
export function configureDestinationPorts(vesselPorts: unknown): void {
  portIndex.clear();
  if (!Array.isArray(vesselPorts)) return;
  for (const row of vesselPorts) {
    if (
      !Array.isArray(row) ||
      row.length !== 6 ||
      ![row[0], row[1], row[2], row[5]].every(
        (value) => typeof value === "string",
      )
    )
      continue;
    const [code, name, country, latitude, longitude, alternate] =
      row as unknown as PortRow;
    if (
      !Number.isFinite(latitude) ||
      !Number.isFinite(longitude) ||
      Math.abs(latitude) > 90 ||
      Math.abs(longitude) > 180 ||
      !name.trim()
    )
      continue;
    const port = { code, name, country, position: { latitude, longitude } };
    const names = [name, alternate].filter(Boolean);
    const keys = new Set(
      [
        ...(code ? [code] : []),
        ...names.flatMap((n) => [
          n,
          `${n} ${country}`,
          `${country} ${n}`,
          ...(code
            ? [`${code.slice(0, 2)} ${n}`, `${n} ${code.slice(0, 2)}`]
            : []),
        ]),
      ].map(normalize),
    );
    for (const key of keys) {
      const existing = portIndex.get(key);
      if (!portIndex.has(key)) portIndex.set(key, port);
      else if (
        !existing ||
        existing.position.latitude !== latitude ||
        existing.position.longitude !== longitude
      )
        portIndex.set(key, null);
    }
  }
}

export function destinationPort(destination: string): DestinationPort | null {
  return portIndex.get(normalize(destination)) ?? null;
}

// A destination connection, not a motion projection or a navigable sailing route.
export function vesselVoyage(
  vessel: Vessel | null,
  data: CommuteSnapshot | null,
  offline = false,
  now = Date.now(),
): VesselVoyage {
  const destination = reportedDestination(vessel);
  const port =
    destination === "Not reported by AIS" ? null : destinationPort(destination);
  const unavailable = (unavailableReason: string): VesselVoyage => ({
    destination,
    port,
    connection: null,
    unavailableReason,
  });
  if (!vessel)
    return unavailable(
      "The vessel is no longer in the current marine response.",
    );
  if (destination === "Not reported by AIS")
    return unavailable("No destination port was reported by AIS.");
  if (!port)
    return unavailable(
      "The reported destination could not be matched to a unique port location.",
    );
  if (offline || data?.feeds.find((f) => f.id === "marine")?.state !== "ready")
    return unavailable(
      "Destination connection unavailable while the marine feed is stale or offline.",
    );
  const seen = vessel.seenAt ? Date.parse(vessel.seenAt) : NaN;
  if (
    !vessel.positionFreshnessKnown ||
    !Number.isFinite(seen) ||
    now - seen > 5 * 60_000 ||
    seen > now + 60_000
  )
    return unavailable(
      "A fresh AIS position report is needed to show the destination connection.",
    );
  const { latitude, longitude } = vessel.position;
  if (
    !Number.isFinite(latitude) ||
    !Number.isFinite(longitude) ||
    Math.abs(latitude) > 90 ||
    Math.abs(longitude) > 180
  )
    return unavailable(
      "The reported ship position cannot be placed on the map.",
    );
  return {
    destination,
    port,
    connection: { points: [vessel.position, port.position] },
    unavailableReason: "",
  };
}
