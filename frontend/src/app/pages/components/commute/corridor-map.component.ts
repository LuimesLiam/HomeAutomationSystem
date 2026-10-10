import {
  AfterViewInit,
  Component,
  ElementRef,
  EventEmitter,
  Inject,
  Input,
  NgZone,
  OnChanges,
  OnDestroy,
  Output,
  PLATFORM_ID,
  ViewChild,
} from "@angular/core";
import { isPlatformBrowser } from "@angular/common";
import type * as Leaflet from "leaflet";
import { vesselVoyage } from "./vessel-voyage";
import { liftSeverity, reportSeverity, severityColors } from "./map-alerts";
import {
  CommuteSnapshot,
  GeoPoint,
  RoadItem,
  TrafficCamera,
  Vessel,
} from "./commute.models";

@Component({
  selector: "app-corridor-map",
  standalone: true,
  template: `
    <button type="button" (click)="resetView()">Reset view</button>
    <p>Drag to pan. Use +/− or pinch to zoom. Select a marker for details.</p>
    <div
      #canvas
      class="corridor-map"
      role="region"
      aria-label="Route map with cameras, road reports, vessels and lift bridge"
    ></div>
    <p role="status" [hidden]="!mapError">{{ mapError }}</p>
  `,
  styles: [
    `
      :host {
        display: block;
        min-width: 0;
      }
      .corridor-map {
        height: min(72vh, 760px);
        min-height: 480px;
        width: 100%;
        border-radius: 12px;
        position: relative;
        z-index: 0;
      }
      p {
        color: #64748b;
        font-size: 0.9rem;
      }
      button {
        padding: 0.65rem 1rem;
        background: white;
        color: #172a3a;
        border: 1px solid #d5e2ef;
        border-radius: 8px;
        cursor: pointer;
      }
      @media (max-width: 600px) {
        .corridor-map {
          height: 58vh;
          min-height: 360px;
        }
      }
    `,
  ],
})
export class CorridorMapComponent
  implements AfterViewInit, OnChanges, OnDestroy
{
  @Input({ required: true }) snapshot!: CommuteSnapshot;
  @Input() offline = false;
  @Input() selectedVesselMmsi: string | null = null;
  @Input() focusVesselRequest = 0;
  @Output() cameraSelected = new EventEmitter<TrafficCamera>();
  @Output() reportSelected = new EventEmitter<RoadItem>();
  @Output() vesselSelected = new EventEmitter<Vessel>();
  @Output() bridgeSelected = new EventEmitter<{
    label: string;
    position: GeoPoint;
  }>();
  @ViewChild("canvas") canvas!: ElementRef<HTMLDivElement>;
  mapError = "";
  private leaflet?: typeof Leaflet;
  private map?: Leaflet.Map;
  private layers?: Leaflet.LayerGroup;
  private tiles?: Leaflet.TileLayer;
  private endpoints: Leaflet.CircleMarker[] = [];
  private routeSignature = "";
  private focusedVesselRequest = 0;
  private resize?: ResizeObserver;
  private disposed = false;
  private mapSize = { width: 0, height: 0 };

  constructor(
    @Inject(PLATFORM_ID) private platform: object,
    private zone: NgZone,
  ) {}

  async ngAfterViewInit() {
    if (!isPlatformBrowser(this.platform)) return;
    try {
      const imported = await import("leaflet");
      // Development and production bundlers expose CommonJS exports differently.
      const L = (imported as { default?: typeof Leaflet }).default ?? imported;
      if (this.disposed) return;
      this.leaflet = L;
      this.zone.runOutsideAngular(() => {
        this.map = L.map(this.canvas.nativeElement, { scrollWheelZoom: false });
        const tiles = L.tileLayer(
          "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
          {
            maxZoom: 19,
            attribution:
              '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
            referrerPolicy: "strict-origin-when-cross-origin",
          },
        );
        tiles.on("tileerror", () =>
          this.zone.run(() => {
            this.mapError =
              "Map tiles could not be loaded. Route and markers are still available; use Reset view to retry.";
          }),
        );
        this.tiles = tiles;
        tiles.addTo(this.map!);
        this.layers = L.layerGroup().addTo(this.map!);
        this.mapSize = {
          width: this.canvas.nativeElement.clientWidth,
          height: this.canvas.nativeElement.clientHeight,
        };
        this.resize = new ResizeObserver(() => {
          const size = {
            width: this.canvas.nativeElement.clientWidth,
            height: this.canvas.nativeElement.clientHeight,
          };
          this.map?.invalidateSize({ pan: false });
          if (
            size.width &&
            size.height &&
            (size.width !== this.mapSize.width ||
              size.height !== this.mapSize.height)
          ) {
            this.mapSize = size;
            this.resetView();
          }
        });
        this.resize.observe(this.canvas.nativeElement);
        this.render();
      });
    } catch {
      this.mapError = "The map could not be loaded. Refresh the page to retry.";
    }
  }

  ngOnChanges() {
    this.zone.runOutsideAngular(() => this.render());
  }

  private render() {
    const L = this.leaflet;
    if (!L || !this.map || !this.layers || !this.snapshot?.route) return;
    const data = this.snapshot;
    const route = data.route!;
    this.layers.clearLayers();
    this.endpoints = [];
    L.polyline(
      route.points.map((p) => this.latLng(p)),
      { color: "#0f766e", weight: 5 },
    ).addTo(this.layers);
    for (const [position, label] of [
      [route.points[0], `Start: ${route.origin}`],
      [route.points[route.points.length - 1], `End: ${route.destination}`],
    ] as [GeoPoint, string][]) {
      const text = document.createElement("span");
      text.textContent = label;
      text.style.cssText =
        "display:block;width:max-content;max-width:150px;white-space:normal";
      const endpoint = L.circleMarker(this.latLng(position), {
        radius: 6,
        color: "#0f766e",
        fillColor: "white",
        fillOpacity: 1,
      })
        .bindTooltip(text, {
          permanent: this.canvas.nativeElement.clientWidth >= 600,
          direction: "top",
          offset: [0, -8],
        })
        .addTo(this.layers);
      this.endpoints.push(endpoint);
    }
    for (const c of data.cameras)
      this.addMarker(c.position, `Camera: ${c.name}`, "#2563eb", "●", () =>
        this.cameraSelected.emit(c),
      );
    for (const item of data.items) {
      // Reports are point markers; their long source geometries are not another trip route.
      const position = item.position ?? item.geometry[0];
      if (position)
        this.addMarker(
          position,
          item.title,
          severityColors[reportSeverity(item, data, this.offline)],
          "!",
          () => this.reportSelected.emit(item),
        );
    }
    const predictions =
      !this.offline &&
      data.feeds.find((f) => f.id === "marine")?.state === "ready"
        ? data.predictions
        : [];
    for (const v of data.vessels) {
      const prediction = predictions.find((p) => p.vesselMmsi === v.mmsi);
      const severity = liftSeverity(prediction?.level ?? "LOW");
      this.addMarker(
        v.position,
        `Vessel: ${v.name}`,
        severity === "neutral" ? "#7c3aed" : severityColors[severity],
        "▲",
        () => this.vesselSelected.emit(v),
        v.course ?? v.heading ?? 0,
      );
    }
    if (data.bridge) {
      const position = data.bridge;
      const label = route.bridge?.name || "Lift bridge";
      const levels = predictions.map((p) => liftSeverity(p.level));
      const severity = levels.includes("danger")
        ? "danger"
        : levels.includes("warning")
          ? "warning"
          : "neutral";
      this.addMarker(
        position,
        label,
        severity === "neutral" ? "#0f172a" : severityColors[severity],
        "B",
        () => this.bridgeSelected.emit({ label, position }),
      );
    }
    const signature = JSON.stringify([route.id, route.points, data.bridge]);
    if (signature !== this.routeSignature) {
      this.routeSignature = signature;
      this.resetView();
    }
    this.renderSelectedVessel();
  }

  private renderSelectedVessel() {
    const L = this.leaflet!;
    const vessel = this.snapshot.vessels.find(
      (v) => v.mmsi === this.selectedVesselMmsi,
    );
    if (!vessel) return;
    L.circleMarker(this.latLng(vessel.position), {
      radius: 14,
      color: "#7c3aed",
      weight: 2,
      fill: false,
      interactive: false,
    }).addTo(this.layers!);
    const voyage = vesselVoyage(vessel, this.snapshot, this.offline);
    let positions: Leaflet.LatLngTuple[] = [this.latLng(vessel.position)];
    if (voyage.connection) {
      // Keep the destination connection continuous when crossing the international date line.
      positions = voyage.connection.points.map((p) => [
        p.latitude,
        vessel.position.longitude +
          ((p.longitude - vessel.position.longitude + 540) % 360) -
          180,
      ]);
      L.polyline(positions, {
        color: "#7c3aed",
        weight: 3,
        dashArray: "7 7",
        interactive: false,
        className: "vessel-destination-connection",
      }).addTo(this.layers!);
      const text = document.createElement("span");
      text.textContent = `Destination port: ${voyage.port!.name}, ${voyage.port!.country}`;
      L.circleMarker(positions[positions.length - 1], {
        radius: 7,
        color: "#7c3aed",
        fillColor: "white",
        fillOpacity: 1,
      })
        .bindTooltip(text, { permanent: true, direction: "top" })
        .addTo(this.layers!);
    }
    if (this.focusedVesselRequest !== this.focusVesselRequest) {
      this.focusedVesselRequest = this.focusVesselRequest;
      this.map!.fitBounds(L.latLngBounds(positions), {
        padding: [45, 45],
        maxZoom: 14,
        animate: false,
      });
    }
  }

  private latLng(p: GeoPoint): Leaflet.LatLngTuple {
    return [p.latitude, p.longitude];
  }

  private addMarker(
    position: GeoPoint,
    label: string,
    color: string,
    symbol: string,
    select: () => void,
    bearing = 0,
  ) {
    const L = this.leaflet!;
    // Labels are assigned as text, never interpolated into provider-supplied HTML.
    const glyph = document.createElement("span");
    glyph.textContent = symbol;
    glyph.style.cssText =
      symbol === "!" || symbol === "B"
        ? `display:block;margin:3px;width:22px;height:22px;border-radius:50%;border:2px solid white;background:${color};color:white;font-size:13px;font-weight:700;line-height:18px;text-align:center;box-shadow:0 1px 4px #0005`
        : `display:block;color:${color};font-size:18px;line-height:28px;text-align:center;text-shadow:0 0 2px white,0 0 3px white;transform:rotate(${bearing}deg)`;
    const marker = L.marker(this.latLng(position), {
      title: label,
      alt: label,
      keyboard: true,
      zIndexOffset:
        symbol === "B"
          ? 2000
          : color === severityColors.danger
            ? 1500
            : color === severityColors.warning
              ? 1000
              : 0,
      icon: L.divIcon({
        html: glyph,
        className: "commute-map-marker",
        iconSize: [28, 28],
        iconAnchor: [14, 14],
      }),
    });
    marker.on("add", () => {
      const element = marker.getElement();
      element?.setAttribute("aria-label", label);
      element?.addEventListener("keydown", (event) => {
        if (event.key === " " || event.key === "Enter") {
          event.preventDefault();
          this.zone.run(select);
        }
      });
    });
    marker.addTo(this.layers!);
    const tooltip = document.createElement("span");
    tooltip.textContent = label;
    marker.bindTooltip(tooltip);
    marker.on("click", () => this.zone.run(select));
  }

  resetView() {
    if (!this.map || !this.leaflet || !this.snapshot?.route) return;
    if (this.mapError) {
      this.mapError = "";
      this.tiles?.redraw();
    }
    const points = [
      ...this.snapshot.route.points,
      ...(this.snapshot.bridge ? [this.snapshot.bridge] : []),
    ];
    this.map.invalidateSize({ pan: false });
    this.map.fitBounds(
      this.leaflet.latLngBounds(points.map((p) => this.latLng(p))),
      {
        padding: [this.canvas.nativeElement.clientWidth < 600 ? 75 : 30, 30],
        maxZoom: 14,
        animate: false,
      },
    );
    for (const endpoint of this.endpoints) {
      const tooltip = endpoint.getTooltip();
      if (!tooltip) continue;
      tooltip.options.permanent = this.canvas.nativeElement.clientWidth >= 600;
      if (tooltip.options.permanent) endpoint.openTooltip();
      else endpoint.closeTooltip();
    }
  }

  ngOnDestroy() {
    this.disposed = true;
    this.resize?.disconnect();
    this.map?.remove();
  }
}
