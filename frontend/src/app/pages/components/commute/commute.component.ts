import { FormsModule } from '@angular/forms';
import { CorridorMapComponent } from './corridor-map.component';
import {
  Component,
  ElementRef,
  Inject,
  OnDestroy,
  OnInit,
  PLATFORM_ID,
  ViewChild,
} from "@angular/core";
import { DomSanitizer, SafeResourceUrl } from "@angular/platform-browser";
import { CommonModule, isPlatformBrowser } from "@angular/common";
import {
  Subject,
  catchError,
  exhaustMap,
  of,
  takeUntil,
  timer,
  merge,
  switchMap,
} from "rxjs";
import { liftSeverity, reportFeedFresh, reportSeverity } from "./map-alerts";
import {
  configureDestinationPorts,
  reportedDestination,
  vesselVoyage,
} from "./vessel-voyage";
import { CommuteService } from "./commute.service";
import {
  CameraView,
  CommuteRoute,
  CommuteSnapshot,
  GeoPoint,
  LiftPrediction,
  TrafficCamera,
  DashboardProfile,
  Workspace,
  RouteMaps,
  FavoriteCameraView,
  RoadItem,
  Vessel,
} from "./commute.models";

@Component({
  selector: "app-commute",
  standalone: true,
  imports: [CommonModule, FormsModule, CorridorMapComponent],
  templateUrl: "./commute.component.html",
  styleUrl: "./commute.component.scss",
})
export class CommuteComponent implements OnInit, OnDestroy {
  @ViewChild("cameraDialog") cameraDialog!: ElementRef<HTMLDialogElement>;
  @ViewChild("mapDetailDialog") mapDetailDialog!: ElementRef<HTMLDialogElement>;
  selectedRoadItem: RoadItem | null = null;
  selectedVessel: Vessel | null = null;
  selectedVesselMmsi: string | null = null;
  vesselFocusRequest = 0;
  selectedBridge = false;
  reversing = false;
  data: CommuteSnapshot | null = null;
  workspace: Workspace | null = null;
  tab = "dashboard";
  profileDraft: DashboardProfile | null = null;
  workspaceError = "";
  profileError = "";
  profileSaving = false;
  maps: RouteMaps | null = null;
  embedSrc: SafeResourceUrl | null = null;
  mapsKey = "";
  mapsSaving = false;
  mapsError = "";
  marineEnabled = false;
  bridgeEnabled = false;
  deletingRoute: string | null = null;
  deletingDashboard: string | null = null;
  readonly panelOptions = [
    { id: "map", name: "Map shortcut" },
    { id: "bridge", name: "Lift bridge outlook" },
    { id: "reports", name: "Road reports" },
    { id: "cameras", name: "Camera views" },
    { id: "marine", name: "Marine traffic" },
  ];
  error = "";
  loading = true;
  editing = false;
  saving = false;
  saveError = "";
  saved = "";
  draft: CommuteRoute | null = null;
  roadwaysText = "";
  alertRegionsText = "";
  pointsText = "";
  selectedCamera: TrafficCamera | null = null;
  selectedView: CameraView | null = null;
  itemFilter = "all";
  failedImages = new Set<string>();
  readonly directions = [
    "All Directions",
    "Eastbound",
    "Westbound",
    "Northbound",
    "Southbound",
    "Inbound",
    "Outbound",
  ];
  private destroyed = new Subject<void>();
  private request = new Subject<void>();
  private timeFormatter = new Intl.DateTimeFormat(undefined, {
    hour: "numeric",
    minute: "2-digit",
    timeZoneName: "short",
  });
  constructor(
    private api: CommuteService,
    private sanitizer: DomSanitizer,
    @Inject(PLATFORM_ID) private platform: object,
  ) {}
  ngOnInit() {
    if (!isPlatformBrowser(this.platform)) return;
    configureDestinationPorts([]);
    this.api
      .ports()
      .pipe(
        catchError(() => of([])),
        takeUntil(this.destroyed),
      )
      .subscribe((ports) => {
        configureDestinationPorts(ports);
        if (this.data) this.data = { ...this.data };
      });
    const fetch = () =>
      this.api.workspace().pipe(
        catchError(() => {
          this.workspaceError =
            "Database settings could not be loaded. Check the server database connection.";
          return of(null);
        }),
        switchMap((workspace) => {
          if (workspace) {
            this.workspace = workspace;
            this.workspaceError = "";
          }
          return this.api.snapshot().pipe(
            catchError(() => {
              this.error =
                "The app server could not be reached. Displayed data may be stale. Retrying automatically.";
              this.loading = false;
              return of(null);
            }),
          );
        }),
      );
    // exhaustMap prevents overlapping requests; polling ends when navigating away.
    merge(timer(0, 30000), this.request)
      .pipe(takeUntil(this.destroyed), exhaustMap(fetch))
      .subscribe((value) => this.receive(value));
  }
  private receive(value: CommuteSnapshot | null) {
    this.loading = false;
    if (!value) return;
    if (
      value.feeds.find((f) => f.id === "cameras")?.lastSuccess !==
      this.data?.feeds.find((f) => f.id === "cameras")?.lastSuccess
    )
      this.failedImages.clear();
    const changedRoute =
      JSON.stringify(value.route) !== JSON.stringify(this.data?.route);
    this.data = value;
    this.error = "";
    if (changedRoute) {
      this.selectedVesselMmsi = null;
      this.selectedVessel = null;
      this.mapDetailDialog?.nativeElement.close();
      this.maps = null;
      this.embedSrc = null;
    }
    if (this.selectedVesselMmsi) {
      const current = value.vessels.find(
        (v) => v.mmsi === this.selectedVesselMmsi,
      );
      if (!current) this.clearSelectedVessel();
      else if (this.selectedVessel) this.selectedVessel = current;
    }
    if (value.route && (!this.maps || changedRoute))
      this.loadMaps(value.route.id);
    if (!value.route && !this.editing) this.tab = "builder";
  }
  ngOnDestroy() {
    this.destroyed.next();
    this.destroyed.complete();
  }
  refresh() {
    this.loading = true;
    this.request.next();
  }
  time(value: string | null | undefined) {
    return value ? this.timeFormatter.format(new Date(value)) : "—";
  }
  date(value: string | null) {
    return value
      ? new Intl.DateTimeFormat(undefined, {
          month: "short",
          day: "numeric",
          hour: "numeric",
          minute: "2-digit",
        }).format(new Date(value))
      : "Not supplied";
  }
  age(value: string | null | undefined) {
    if (!value) return "Never";
    const minutes = Math.max(
      0,
      Math.floor((Date.now() - new Date(value).getTime()) / 60000),
    );
    return minutes < 1
      ? "Just now"
      : minutes < 60
        ? `${minutes} min ago`
        : `${Math.floor(minutes / 60)}h ${minutes % 60}m ago`;
  }
  get visibleItems() {
    return (
      this.data?.items.filter(
        (i) => this.itemFilter === "all" || i.kind === this.itemFilter,
      ) ?? []
    );
  }
  get upcomingPredictions() {
    return this.data?.predictions.filter((p) => p.level !== "LOW") ?? [];
  }
  prediction(mmsi: string): LiftPrediction | undefined {
    return this.data?.predictions.find((p) => p.vesselMmsi === mmsi);
  }
  feed(id: string) {
    return this.data?.feeds.find((f) => f.id === id);
  }
  statusClass(value: string) {
    return value.toLowerCase().replaceAll(" ", "-");
  }
  enabledViews(camera: TrafficCamera) {
    return camera.views.filter((v) => v.enabled);
  }
  cameraImage(view: CameraView) {
    const stamp = this.feed("cameras")?.lastSuccess;
    return stamp
      ? `${view.url}${view.url.includes("?") ? "&" : "?"}_commute=${encodeURIComponent(stamp)}`
      : view.url;
  }
  imageFailed(id: string) {
    this.failedImages.add(id);
  }
  openCamera(camera: TrafficCamera, view?: CameraView) {
    this.selectedCamera = camera;
    this.selectedView = view ?? this.enabledViews(camera)[0] ?? null;
    this.cameraDialog.nativeElement.showModal();
  }
  closeCamera() {
    this.cameraDialog.nativeElement.close();
  }
  editRoute(route: CommuteRoute | null | undefined = this.data?.route) {
    if (!route) {
      route = {
        id: "00000000-0000-0000-0000-000000000000",
        name: "",
        origin: "",
        destination: "",
        roadways: [],
        alertRegions: [],
        direction: "All Directions",
        corridorKm: 2,
        points: [],
        marineBounds: null,
        bridge: null,
      };
    }
    this.marineEnabled = !!route.marineBounds;
    this.bridgeEnabled = !!route.bridge;
    this.tab = "builder";
    this.draft = structuredClone(route);
    this.roadwaysText = route.roadways.join(", ");
    this.alertRegionsText = route.alertRegions.join(", ");
    this.pointsText = route.points
      .map((p) => `${p.latitude}, ${p.longitude}`)
      .join("\n");
    this.saveError = "";
    this.editing = true;
    this.saved = "";
  }
  reverseRoute() {
    if (!this.draft) return;
    [this.draft.origin, this.draft.destination] = [
      this.draft.destination,
      this.draft.origin,
    ];
    this.pointsText = this.pointsText.trim().split("\n").reverse().join("\n");
    this.draft.name = `${this.draft.origin} → ${this.draft.destination}`;
    const opposite: Record<string, string> = {
      Eastbound: "Westbound",
      Westbound: "Eastbound",
      Northbound: "Southbound",
      Southbound: "Northbound",
      Inbound: "Outbound",
      Outbound: "Inbound",
    };
    this.draft.direction = opposite[this.draft.direction] ?? "All Directions";
  }
  saveRoute() {
    if (!this.draft || this.saving) return;
    this.saveError = "";
    const points = this.pointsText
      .trim()
      .split("\n")
      .map((line) =>
        line
          .trim()
          .split(",")
          .map((n) => n.trim()),
      );
    if (
      points.some(
        (p) =>
          p.length !== 2 ||
          p.some((n) => n === "" || !Number.isFinite(Number(n))),
      )
    ) {
      this.saveError = "Enter one latitude, longitude pair per line.";
      return;
    }
    const route = {
      ...this.draft,
      marineBounds: this.marineEnabled ? this.draft.marineBounds : null,
      bridge: this.bridgeEnabled ? this.draft.bridge : null,
      alertRegions: this.alertRegionsText
        .split(",")
        .map((s) => s.trim())
        .filter(Boolean),
      roadways: this.roadwaysText
        .split(",")
        .map((s) => s.trim())
        .filter(Boolean),
      points: points.map((p) => ({
        latitude: Number(p[0]),
        longitude: Number(p[1]),
      })),
    };
    this.saving = true;
    this.api
      .saveRoute(route)
      .pipe(takeUntil(this.destroyed))
      .subscribe({
        next: () => {
          this.saving = false;
          this.editing = false;
          this.saved =
            "Route saved. Feeds will follow the next scheduled refresh.";
          this.tab = "dashboard";
          this.refresh();
        },
        error: (e) => {
          this.saving = false;
          this.saveError =
            e.error?.message ??
            e.error?.detail ??
            "Could not save the route. Check the values and server connection.";
        },
      });
  }
  get activeProfile() {
    return this.data?.dashboard ?? null;
  }
  get selectableRoutes() {
    const ids = this.activeProfile?.routeIds;
    return ids
      ? (this.workspace?.routes.filter((r) => ids.includes(r.id)) ?? [])
      : (this.workspace?.routes ?? []);
  }
  useRoute(
    routeId: string,
    dashboardId = this.workspace?.activeDashboardId ?? null,
  ) {
    this.api
      .activate(routeId, dashboardId)
      .pipe(takeUntil(this.destroyed))
      .subscribe({
        next: (w) => {
          this.workspace = w;
          this.maps = null;
          this.embedSrc = null;
          if (this.tab !== "map") this.tab = "dashboard";
          this.refresh();
        },
        error: (e) =>
          (this.workspaceError =
            e.error?.message ?? e.error?.detail ?? "Could not switch routes."),
      });
  }
  useDashboard(id: string) {
    const profile = this.workspace?.dashboards.find((p) => p.id === id);
    if (!profile?.routeIds.length) {
      if (profile) this.editDashboard(profile);
      return;
    }
    const routeId = profile.routeIds.includes(
      this.workspace?.activeRouteId ?? "",
    )
      ? this.workspace!.activeRouteId!
      : profile.routeIds[0];
    this.useRoute(routeId, id);
  }
  editDashboard(profile?: DashboardProfile | null) {
    this.tab = "builder";
    this.profileError = "";
    this.profileDraft = profile
      ? structuredClone(profile)
      : {
          id: "00000000-0000-0000-0000-000000000000",
          name: "",
          routeIds: this.data?.route ? [this.data.route.id] : [],
          favoriteViews: [],
          panels: this.panelOptions.map((p) => p.id),
          favoritesOnly: false,
        };
  }
  toggleFavoriteRoute(id: string, selected: boolean) {
    if (!this.profileDraft) return;
    this.profileDraft.routeIds = selected
      ? [...this.profileDraft.routeIds, id]
      : this.profileDraft.routeIds.filter((r) => r !== id);
  }
  togglePanel(id: string, selected: boolean) {
    if (!this.profileDraft) return;
    this.profileDraft.panels = selected
      ? [...this.profileDraft.panels, id]
      : this.profileDraft.panels.filter((p) => p !== id);
  }
  panelName(id: string) {
    return this.panelOptions.find((p) => p.id === id)?.name ?? id;
  }
  movePanel(index: number, offset: number) {
    const panels = this.profileDraft?.panels;
    if (!panels || index + offset < 0 || index + offset >= panels.length)
      return;
    [panels[index], panels[index + offset]] = [
      panels[index + offset],
      panels[index],
    ];
  }
  panelVisible(id: string) {
    return !this.activeProfile || this.activeProfile.panels.includes(id);
  }
  panelOrder(id: string) {
    return (
      this.activeProfile?.panels ?? this.panelOptions.map((p) => p.id)
    ).indexOf(id);
  }
  isFavorite(cameraId: string, viewId: string, draft = false) {
    const profile = draft ? this.profileDraft : this.activeProfile;
    return !!profile?.favoriteViews.some(
      (f) => f.cameraId === cameraId && f.viewId === viewId,
    );
  }
  toggleView(cameraId: string, viewId: string, draft = true) {
    if (draft) {
      if (!this.profileDraft) return;
      this.profileDraft.favoriteViews = this.isFavorite(cameraId, viewId, true)
        ? this.profileDraft.favoriteViews.filter(
            (f) => !(f.cameraId === cameraId && f.viewId === viewId),
          )
        : [...this.profileDraft.favoriteViews, { cameraId, viewId }];
    } else {
      const profile = structuredClone(
        this.activeProfile ?? {
          id: "00000000-0000-0000-0000-000000000000",
          name: "My dashboard",
          routeIds: this.data?.route ? [this.data.route.id] : [],
          favoriteViews: [],
          panels: this.panelOptions.map((p) => p.id),
          favoritesOnly: false,
        },
      );
      profile.favoriteViews = this.isFavorite(cameraId, viewId)
        ? profile.favoriteViews.filter(
            (f) => !(f.cameraId === cameraId && f.viewId === viewId),
          )
        : [...profile.favoriteViews, { cameraId, viewId }];
      this.persistProfile(profile);
    }
  }
  get cameraCards() {
    const cameras = this.data?.cameras ?? [];
    if (this.activeProfile?.favoritesOnly)
      return this.activeProfile.favoriteViews.flatMap((f) => {
        const camera = cameras.find((c) => c.id === f.cameraId);
        const view = camera?.views.find((v) => v.id === f.viewId && v.enabled);
        return camera && view ? [{ camera, view }] : [];
      });
    return cameras.map((camera) => ({
      camera,
      view:
        this.enabledViews(camera).find((v) =>
          this.isFavorite(camera.id, v.id),
        ) ?? this.enabledViews(camera)[0],
    }));
  }
  favoriteLabel(f: FavoriteCameraView) {
    const camera = this.data?.cameras.find((c) => c.id === f.cameraId);
    const view = camera?.views.find((v) => v.id === f.viewId);
    return camera
      ? `${camera.name} · ${view?.description || "View " + f.viewId}`
      : `Camera ${f.cameraId} · View ${f.viewId} (outside current route or unavailable)`;
  }
  removeDraftView(f: FavoriteCameraView) {
    this.profileDraft!.favoriteViews = this.profileDraft!.favoriteViews.filter(
      (v) => v !== f,
    );
  }
  saveProfile() {
    if (this.profileDraft) this.persistProfile(this.profileDraft);
  }
  private persistProfile(profile: DashboardProfile) {
    if (this.profileSaving) return;
    this.profileSaving = true;
    this.profileError = "";
    this.api
      .saveDashboard(profile)
      .pipe(takeUntil(this.destroyed))
      .subscribe({
        next: (w) => {
          this.workspace = w;
          this.profileSaving = false;
          this.profileDraft = null;
          this.tab = "dashboard";
          this.saved = "Dashboard saved to the database.";
          this.refresh();
        },
        error: (e) => {
          this.profileSaving = false;
          this.profileError =
            e.error?.message ?? e.error?.detail ?? "Could not save dashboard.";
        },
      });
  }
  deleteRoute(id: string) {
    this.api
      .deleteRoute(id)
      .pipe(takeUntil(this.destroyed))
      .subscribe({
        next: (w) => {
          this.workspace = w;
          this.deletingRoute = null;
          this.refresh();
        },
        error: () => (this.workspaceError = "Could not delete route."),
      });
  }
  deleteDashboard(id: string) {
    this.api
      .deleteDashboard(id)
      .pipe(takeUntil(this.destroyed))
      .subscribe({
        next: (w) => {
          this.workspace = w;
          this.deletingDashboard = null;
          this.refresh();
        },
        error: () => (this.workspaceError = "Could not delete dashboard."),
      });
  }
  enableMarine(enabled: boolean) {
    this.marineEnabled = enabled;
    if (enabled && this.draft && !this.draft.marineBounds) {
      this.draft.marineBounds = {
        minLatitude: 0,
        minLongitude: 0,
        maxLatitude: 0,
        maxLongitude: 0,
      };
    }
  }
  enableBridge(enabled: boolean) {
    this.bridgeEnabled = enabled;
    if (enabled && this.draft && !this.draft.bridge)
      this.draft.bridge = {
        name: "",
        position: { latitude: 0, longitude: 0 },
        inboundBearing: 0,
      };
  }
  loadMaps(routeId: string) {
    this.api
      .maps(routeId)
      .pipe(takeUntil(this.destroyed))
      .subscribe({
        next: (maps) => {
          if (this.data?.route?.id === routeId) this.maps = maps;
        },
        error: () => (this.mapsError = "Route map is unavailable."),
      });
  }
  showGoogleMap() {
    if (!this.maps?.embedUrl) return;
    const url = new URL(this.maps.embedUrl);
    if (
      url.origin !== "https://www.google.com" ||
      url.pathname !== "/maps/embed/v1/directions"
    )
      return;
    this.embedSrc = this.sanitizer.bypassSecurityTrustResourceUrl(
      url.toString(),
    );
  }
  saveMapsKey() {
    this.mapsSaving = true;
    this.mapsError = "";
    this.api
      .saveMapsKey(this.mapsKey.trim())
      .pipe(takeUntil(this.destroyed))
      .subscribe({
        next: (w) => {
          this.workspace = w;
          this.mapsSaving = false;
          this.mapsKey = "";
          this.embedSrc = null;
          this.maps = null;
          if (this.data?.route) this.loadMaps(this.data.route.id);
          this.saved = "Google Maps configuration saved to the database.";
        },
        error: (e) => {
          this.mapsSaving = false;
          this.mapsError =
            e.error?.message ??
            e.error?.detail ??
            "Could not save Maps configuration.";
        },
      });
  }
  mapLink(p: GeoPoint) {
    return `https://www.openstreetmap.org/?mlat=${p.latitude}&mlon=${p.longitude}#map=14/${p.latitude}/${p.longitude}`;
  }
  openRoadItem(item: RoadItem) {
    this.selectedBridge = false;
    this.selectedRoadItem = item;
    this.selectedVessel = null;
    this.mapDetailDialog.nativeElement.showModal();
  }
  openVessel(vessel: Vessel) {
    this.selectedBridge = false;
    this.selectedVessel = vessel;
    this.selectedVesselMmsi = vessel.mmsi;
    this.selectedRoadItem = null;
    this.mapDetailDialog.nativeElement.showModal();
  }
  get mapVessel() {
    return (
      this.data?.vessels.find((v) => v.mmsi === this.selectedVesselMmsi) ?? null
    );
  }
  voyage(vessel: Vessel) {
    return vesselVoyage(vessel, this.data, !!this.error);
  }
  destination(vessel: Vessel) {
    return reportedDestination(vessel);
  }
  showSelectedVesselOnMap() {
    if (!this.mapVessel) return;
    this.tab = "map";
    this.vesselFocusRequest++;
    this.mapDetailDialog.nativeElement.close();
  }
  clearSelectedVessel() {
    if (this.selectedVessel) this.mapDetailDialog?.nativeElement.close();
    this.selectedVessel = null;
    this.selectedVesselMmsi = null;
  }
  reverseActiveRoute() {
    if (!this.data?.route || this.reversing) return;
    this.reversing = true;
    this.workspaceError = "";
    this.api
      .reverseRoute(this.data.route.id)
      .pipe(takeUntil(this.destroyed))
      .subscribe({
        next: (workspace) => {
          this.workspace = workspace;
          this.reversing = false;
          this.maps = null;
          this.embedSrc = null;
          this.refresh();
        },
        error: (e) => {
          this.reversing = false;
          this.workspaceError =
            e.error?.message ??
            e.error?.detail ??
            "Could not reverse the trip. Try again.";
        },
      });
  }
  reportIsFresh(item: RoadItem) {
    return reportFeedFresh(item, this.data, !!this.error);
  }
  reportHighlight(item: RoadItem) {
    return reportSeverity(item, this.data, !!this.error);
  }
  liftHighlight(level: string) {
    return liftSeverity(level);
  }
  get mapPredictions() {
    return !this.error && this.feed("marine")?.state === "ready"
      ? this.upcomingPredictions
      : [];
  }
  openBridge() {
    this.selectedBridge = true;
    this.selectedRoadItem = null;
    this.selectedVessel = null;
    this.mapDetailDialog.nativeElement.showModal();
  }
  openPrediction(prediction: LiftPrediction) {
    const vessel = this.data?.vessels.find(
      (v) => v.mmsi === prediction.vesselMmsi,
    );
    if (vessel) this.openVessel(vessel);
  }
}
