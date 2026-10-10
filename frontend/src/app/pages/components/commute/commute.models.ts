export interface GeoPoint {
  latitude: number;
  longitude: number;
}
export interface MarineBounds {
  minLatitude: number;
  minLongitude: number;
  maxLatitude: number;
  maxLongitude: number;
}
export interface CommuteRoute {
  id: string;
  bridge: BridgeConfiguration | null;
  name: string;
  origin: string;
  destination: string;
  roadways: string[];
  alertRegions: string[];
  direction: string;
  corridorKm: number;
  points: GeoPoint[];
  marineBounds: MarineBounds | null;
}
export interface FeedStatus {
  id: string;
  name: string;
  state: string;
  lastSuccess: string | null;
  lastAttempt: string | null;
  nextAttempt: string | null;
  message: string;
}
export interface CameraView {
  id: string;
  url: string;
  description: string;
  enabled: boolean;
}
export interface TrafficCamera {
  id: string;
  name: string;
  roadway: string;
  direction: string;
  position: GeoPoint;
  views: CameraView[];
}
export interface RoadItem {
  id: string;
  kind: string;
  title: string;
  description: string;
  roadway: string;
  direction: string;
  position: GeoPoint | null;
  geometry: GeoPoint[];
  updatedAt: string | null;
  startsAt: string | null;
  endsAt: string | null;
  fullClosure: boolean;
  lanes: string;
  schedule: string;
  regional: boolean;
  advisory: boolean;
}
export interface Vessel {
  mmsi: string;
  name: string;
  position: GeoPoint;
  course: number | null;
  heading: number | null;
  speedKnots: number | null;
  typeCode: number | null;
  type: string;
  lengthMetres: number | null;
  beamMetres: number | null;
  destination: string;
  seenAt: string | null;
  source: string;
  navigationStatus: number | null;
  positionFreshnessKnown: boolean;
}
export interface LiftPrediction {
  level: string;
  vesselMmsi: string;
  vesselName: string;
  direction: string;
  windowStart: string | null;
  windowEnd: string | null;
  confidence: number;
  explanation: string;
}
export interface CommuteSnapshot {
  dashboard: DashboardProfile | null;
  settingsAvailable: boolean;
  route: CommuteRoute | null;
  status: string;
  summary: string;
  cameras: TrafficCamera[];
  items: RoadItem[];
  vessels: Vessel[];
  predictions: LiftPrediction[];
  feeds: FeedStatus[];
  bridge: GeoPoint | null;
  generatedAt: string;
  attribution: string[];
}

export interface BridgeConfiguration {
  name: string;
  position: GeoPoint;
  inboundBearing: number;
}
export interface FavoriteCameraView {
  cameraId: string;
  viewId: string;
}
export interface DashboardProfile {
  id: string;
  name: string;
  routeIds: string[];
  favoriteViews: FavoriteCameraView[];
  panels: string[];
  favoritesOnly: boolean;
}
export interface Workspace {
  routes: CommuteRoute[];
  dashboards: DashboardProfile[];
  activeRouteId: string | null;
  activeDashboardId: string | null;
  googleMapsConfigured: boolean;
}
export interface RouteMaps {
  directionsUrl: string;
  embedUrl: string | null;
}
