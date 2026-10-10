import type { PortRow } from "./vessel-voyage";
import { Injectable } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { buildApiUrl } from "../../../shared/api.constants";
import {
  CommuteRoute,
  CommuteSnapshot,
  Workspace,
  DashboardProfile,
  RouteMaps,
} from "./commute.models";

@Injectable({ providedIn: "root" })
export class CommuteService {
  private readonly url = buildApiUrl("/commute");
  constructor(private http: HttpClient) {}
  ports() {
    return this.http.get<PortRow[]>("assets/vessel-ports.private.json");
  }
  snapshot() {
    return this.http.get<CommuteSnapshot>(this.url);
  }
  reverseRoute(routeId: string) {
    return this.http.put<Workspace>(`${this.url}/route/reverse`, { routeId });
  }
  saveRoute(route: CommuteRoute) {
    return this.http.put<CommuteRoute>(`${this.url}/route`, route);
  }
  workspace() {
    return this.http.get<Workspace>(`${this.url}/workspace`);
  }
  saveDashboard(profile: DashboardProfile) {
    return this.http.put<Workspace>(`${this.url}/dashboards`, profile);
  }
  activate(routeId: string, dashboardId: string | null) {
    return this.http.put<Workspace>(`${this.url}/active`, {
      routeId,
      dashboardId,
    });
  }
  deleteRoute(id: string) {
    return this.http.delete<Workspace>(`${this.url}/routes/${id}`);
  }
  deleteDashboard(id: string) {
    return this.http.delete<Workspace>(`${this.url}/dashboards/${id}`);
  }
  saveMapsKey(key: string) {
    return this.http.put<Workspace>(`${this.url}/maps/key`, { key });
  }
  maps(routeId: string) {
    return this.http.get<RouteMaps>(`${this.url}/maps`, {
      params: { routeId },
    });
  }
}
