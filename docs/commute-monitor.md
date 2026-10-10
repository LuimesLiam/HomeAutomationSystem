# Commute Monitor and personal dashboards

Open **Commute** in the sidebar or `/commute`. **My dashboard** shows the selected saved route. **Map** provides a full-size interactive route map with selectable cameras, road reports, vessels, bridge predictions and an issue list. **Reverse trip** swaps the selected route endpoints, waypoint order and travel direction in PostgreSQL with one click; the same button switches back. **Build my dashboard** lets you create multiple dashboard profiles, choose favorite routes, pin individual camera views, show only pinned views, and choose which panels appear and in what order. Favorite views outside the current route stay saved and appear when switching to the relevant route. The route editor supports named corridors, waypoints, roadway aliases, optional marine bounds and an optional lift bridge with an inbound canal bearing. There is no geographic preset in the application source.

## Database and privacy

Routes, addresses/labels, waypoints, monitoring bounds, bridge locations, dashboard profiles, pinned camera/view IDs, active selections and the Google Maps Embed key are stored in PostgreSQL through the existing database configuration (`POSTGRES_CONNECTION` / `ConnectionStrings:DefaultConnection`). Route settings are no longer written to JSON files. An empty database opens with no route or location configured.

The generic EF model bootstraps two module-specific tables without inserting any location data. Personal data migrations are `.sql` files under **`.devcontainer/private-migrations/commute/`**. `/.devcontainer/private-migrations/` is excluded by `.gitignore` and `.dockerignore`. A private initial seed has been created locally to preserve the previously configured corridor, without including its content in tracked source, tests, templates, docs or frontend fixtures. Keep these files in your own private backup; Git will not transport them to another machine.

On initialization the module applies private SQL migrations in filename order, once per database. Applied names and SHA-256 hashes are recorded in `CommutePrivateMigrations`; changing an already-applied script is rejected. Add a new numbered script for subsequent changes. Each script executes in a transaction. Private SQL is executed without emitting its text through EF command logging. Generic schema remains reproducible from the tracked model when private seeds are absent.

Compose mounts the private migration directory read-only at `/private-migrations`, with `Commute__PrivateMigrationsPath=/private-migrations/commute`. Override the host directory with `COMMUTE_PRIVATE_MIGRATIONS_HOST_PATH`. The scripts are excluded from the application image. Other hosting arrangements can set `Commute__PrivateMigrationsPath` explicitly; local development discovers `.devcontainer/private-migrations/commute` by walking upward from the host content root (the previous root-level path is also supported). Scripts are trusted database-admin input, not downloadable application content.

Database outages during initialization leave the module unavailable with an explicit message and an automatic retry; write failures return a clear error. They do not silently fall back to a local personal-settings file. Back up the database and private migrations. The module follows the existing app's trusted-home-network access model.

## Route map

The Map tab uses the open-source Leaflet library and standard OpenStreetMap tiles with visible attribution. No API key is required. Route and bridge coordinates come from the saved database configuration; camera, report and vessel coordinates come from the live feeds. The saved waypoint line is a monitoring corridor, not calculated driving directions. The map fits the route and bridge on initial load, route changes or **Reset view**; feed refreshes preserve your zoom and pan. Only the saved trip line is drawn; report geometries are represented by markers. Red marks reported closures/incidents and likely or imminent lifts; amber marks restrictions, scheduled closures and possible lifts. Unconfirmed road feeds turn reports gray, and unconfirmed AIS suppresses lift highlights. Schedules remain visible in the issue list and details. Select markers by mouse or keyboard to open details. Drag, pinch or use the zoom buttons; ordinary mouse-wheel scrolling stays with the page.

Opening the map requests tiles from OpenStreetMap, which receives the browser IP and viewed tile area. Public tiles are used under the [OpenStreetMap tile usage policy](https://operations.osmfoundation.org/policies/tiles/); no prefetch or offline download is enabled. Tile failures leave route markers available and show an error.

Selecting a vessel shows its reported AIS destination (or an explicit missing-data label). **View ship on map** focuses the ship and the reported destination port, connected by a dotted purple line with a labelled port marker. Exact UN/LOCODEs (including spaces or hyphens), exact port names, and country-qualified names resolve against an optional deployment port gazetteer. Ambiguous names, unknown codes and missing destinations produce an explanation instead of a guessed line. The connection shows the destination, not a navigable sailing route or the ship's actual planned path. Coordinates are approximate port locations, not berths. AIS destination text may be abbreviated or outdated.

No port names or coordinates are bundled in tracked source. To enable destination lookup, run `python frontend/scripts/import-vessel-ports.py /path/to/world_port_index.csv` with your own CSV. It writes the ignored `frontend/src/assets/vessel-ports.private.json`, which the browser loads on opening the commute page. Without that file, destination text remains available but no port connection is guessed. The file is served to app users, so use only a catalogue you intend to make available to them. Do not commit the generated asset. Rebuild the frontend after importing it.

The destination connection requires a ready marine feed and a known, valid AIS position report within five minutes. Course and speed are not required, so berthed ships can still show their reported destination. Feed refreshes update the selected vessel and destination without moving the user's view. Selection and connection lines are temporary browser state.


## Optional Google Maps

Every saved route gets a [Google Maps directions URL](https://developers.google.com/maps/documentation/urls/get-started), which works without an API key. Select **Map → Google route options → Open route in Google Maps**. Up to three intermediate points are used for mobile compatibility.

For an embedded map, enable Google's [Maps Embed API](https://developers.google.com/maps/documentation/embed/get-api-key), restrict the key to your site's HTTP referrers and the Embed API, and save it in **Build my dashboard → Google Maps**. Workspace responses return only whether a key is configured. The embedded URL necessarily includes a browser-visible key; it is not a server-only secret. An empty key removes the saved configuration.

Select **Load Google route map** to load the iframe. Merely opening the dashboard does not contact Google Maps. Loading a map or opening the external link sends the selected route coordinates to Google. The embed includes up to twenty intermediate points and shows Google's calculated driving directions, which may differ from the saved monitoring corridor. Camera, incident, vessel and bridge markers remain on the separate geographic overview. No paid Routes/Directions service or automatic geocoding is invoked, and Google routing output is not used to overwrite the saved route.

## Live sources

Set `ROAD_TRAFFIC_API_BASE_URL` to your provider's HTTPS API root (including its API path prefix) and `ROAD_TRAFFIC_API_KEY` to its key in your ignored local environment. No endpoint or region is preset. Enter the provider's alert region names in the route editor. The adapter expects compatible `/v2/get/event`, `/v2/get/constructionprojects`, `/v2/get/cameras`, `/v3/get/roadconditions` and `/v2/get/alerts` response formats. Missing endpoint or credentials have an explicit unconfigured state.

Open Waters AIS uses the [documented vessel GeoJSON API](https://openwaters.io/api/ais/) and needs no token for a small bounding box. Monitoring is only enabled for a route with saved marine bounds. Successful empty responses do not confirm an empty area or complete receiver coverage.

Each road traffic source refreshes at most once every 120 seconds; AIS at most once every 60 seconds. One process-wide worker shares caches across dashboard viewers. Dashboard refreshes and route edits do not bypass upstream throttling. Route selection re-filters the shared road cache; AIS data from a previous monitoring area is withheld until a successful refresh for the new area. The browser checks every 30 seconds and stops polling when navigating away.

Reports require matching roadway aliases and geographic corridor relevance, with encoded polylines and secondary endpoints supported. Regional alerts are explicitly labeled broader than the route. Reports show provided schedules and dates, rather than claiming that a recurring closure is active at the current minute. Cameras follow route order; favorite views can be enlarged or opened at the provider source. Camera image capture timestamps are not supplied by the provider.

Provider failures retain cached road/vessel data with stale messages and suppress bridge estimates. A road feed overdue by five minutes or an AIS feed overdue by three minutes is stale. Incomplete road data cannot produce a reassuring “no reported issues” status.

## Lift estimates and Phase 1 limits

Bridge position, name and inbound bearing come from the selected route's database configuration. Course, speed and position determine whether a vessel is approaching the canal; type and length are proxies for lift need. Missing/stale positions, static-data-only updates, stationary vessels, unknown course and passing trajectories cannot produce an approach estimate. AIS report freshness must be under five minutes. Windows include uncertainty and approach lead time, with LOW / POSSIBLE / LIKELY / IMMINENT labels.

AIS does not provide air draught, pilot intentions or operator decisions. Confidence is a heuristic score capped at 85%, not a calibrated probability. Estimates are advisory, and they apply to the configured lift crossing, which may be separate from the monitored roadway. Phase 1 does not estimate road speeds or commute travel time. CV analysis and 3D visualization remain unimplemented.

## API and tests

- `GET /api/commute`: cached selected-route dashboard.
- `GET /api/commute/workspace`: saved routes/profiles and active IDs, without the Maps key.
- `PUT /api/commute/route`: add/update a validated route in the database.
- `PUT /api/commute/route/reverse`: reverse the active route in the database, rejecting a stale route ID.
- `PUT /api/commute/dashboards`: add/update a dashboard profile.
- `PUT /api/commute/active`: choose a route/dashboard.
- `DELETE /api/commute/routes/{id}` and `/dashboards/{id}`: remove saved entries and references.
- `PUT /api/commute/maps/key`: save/remove the Embed key.
- `GET /api/commute/maps?routeId={id}`: safe Google directions/embed URLs for a saved route.

Responses containing settings use `Cache-Control: no-store`. Invalid settings return 400, concurrent database edits 409, and database write failures 503.

Run `node --experimental-strip-types --test frontend/e2e/vessel-voyage.test.mjs` for port matching, destination connection endpoints and data-quality checks. Run `dotnet test backend/Core/HomeApp.Commute.Tests/HomeApp.Commute.Tests.csproj`, `dotnet build backend/Core/HomeApp.Host/HomeApp.Host.csproj`, and `npm run build -- --configuration production` from `frontend`. Database tests use synthetic coordinates and an isolated SQLite database, covering persistence/restart, private migration replay protection, empty-state privacy, favorites and panel order, Google URL construction/key removal, invalid input, cache sharing/outage recovery and deletion cleanup. Set `COMMUTE_UI_ONLY=1` to run browser fixture checks without a backend process. The optional `frontend/e2e/commute-smoke.mjs` uses Playwright with a disposable database or browser-only intercepted settings; production providers include no demo data.
