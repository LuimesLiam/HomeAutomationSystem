// Optional UI smoke test. All mutations use browser-intercepted synthetic data.
// Install Playwright outside the app and point PLAYWRIGHT_MODULE_PATH to index.mjs.
import assert from "node:assert/strict";
const { chromium } = await import(
  process.env.PLAYWRIGHT_MODULE_PATH || "playwright"
);
const base = process.env.COMMUTE_BASE_URL || "http://127.0.0.1:5310";
const apiBase = process.env.COMMUTE_API_BASE_URL || base;
const browser = await chromium.launch({
  headless: true,
  args: ["--no-sandbox"],
});
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
const errors = [];
page.on("pageerror", (error) => errors.push(error.message));
const id = "11111111-1111-1111-1111-111111111111";
const profileId = "22222222-2222-2222-2222-222222222222";
const route = {
  id,
  name: "Synthetic corridor",
  origin: "Start",
  destination: "End",
  roadways: ["A1"],
  alertRegions: [],
  direction: "Eastbound",
  corridorKm: 0.5,
  points: [
    { latitude: 0, longitude: 0 },
    { latitude: 0.01, longitude: 0.01 },
  ],
  marineBounds: null,
  bridge: {
    name: "Synthetic lift bridge",
    position: { latitude: 0.004, longitude: 0.002 },
    inboundBearing: 225,
  },
};
const workspace = {
  routes: [route],
  dashboards: [],
  activeRouteId: id,
  activeDashboardId: null,
  googleMapsConfigured: false,
};
const camera = {
  id: "camera-1",
  name: "Synthetic camera",
  roadway: "A1",
  direction: "Eastbound",
  position: { latitude: 0, longitude: 0 },
  views: [
    {
      id: "view-1",
      url: `${base}/test-camera.svg`,
      description: "Main view",
      enabled: true,
    },
    {
      id: "view-2",
      url: `${base}/missing-camera.jpg`,
      description: "Alternate view",
      enabled: true,
    },
  ],
};
let mapsKey = "";
let feedsReady = true;
let shipOverrides = {};
function snapshot() {
  const selected =
    workspace.routes.find((r) => r.id === workspace.activeRouteId) ?? null;
  return {
    route: selected,
    dashboard:
      workspace.dashboards.find((p) => p.id === workspace.activeDashboardId) ??
      null,
    status: "NO REPORTED ISSUES",
    summary: "Synthetic test data",
    cameras: selected ? [camera] : [],
    items: [
      {
        id: "dry-1",
        kind: "condition",
        title: "Synthetic dry road",
        description: "Bare and dry",
        roadway: "A1",
        direction: "Eastbound",
        position: { latitude: 0.007, longitude: 0.001 },
        geometry: [],
        updatedAt: null,
        startsAt: null,
        endsAt: null,
        fullClosure: false,
        lanes: "",
        schedule: "",
        regional: false,
        advisory: false,
      },
      {
        id: "closure-1",
        kind: "incident",
        title: "Synthetic full closure",
        description: "Reported full closure",
        roadway: "A1",
        direction: "Eastbound",
        position: { latitude: 0.003, longitude: 0.006 },
        geometry: [],
        updatedAt: null,
        startsAt: null,
        endsAt: null,
        fullClosure: true,
        lanes: "All lanes",
        schedule: "Mornings",
        regional: false,
        advisory: true,
      },
      {
        id: "report-1",
        kind: "construction",
        title: "Synthetic lane works",
        description: "One lane closed for repairs.",
        roadway: "A1",
        direction: "Eastbound",
        position: { latitude: 0.006, longitude: 0.006 },
        geometry: [
          { latitude: 0, longitude: 0 },
          { latitude: 0.01, longitude: 0.01 },
        ],
        updatedAt: new Date().toISOString(),
        startsAt: null,
        endsAt: null,
        fullClosure: false,
        lanes: "Right lane",
        schedule: "Overnight",
        regional: false,
        advisory: false,
      },
    ],
    vessels: [
      {
        mmsi: "123456789",
        name: "Synthetic ship",
        position: { latitude: 0.008, longitude: 0.003 },
        course: 120,
        heading: 121,
        speedKnots: 7.2,
        typeCode: 70,
        type: "Cargo",
        lengthMetres: 100,
        beamMetres: 15,
        destination: "XXAAA",
        seenAt: new Date().toISOString(),
        source: "Synthetic AIS",
        navigationStatus: 0,
        positionFreshnessKnown: true,
        ...shipOverrides,
      },
    ],
    predictions: [
      {
        level: "POSSIBLE",
        vesselMmsi: "123456789",
        vesselName: "Synthetic ship",
        direction: "Inbound",
        windowStart: new Date().toISOString(),
        windowEnd: new Date(Date.now() + 600000).toISOString(),
        confidence: 40,
        explanation: "Synthetic possible lift",
      },
    ],
    feeds: [
      "cameras",
      "incidents",
      "construction",
      "conditions",
      "alerts",
      "marine",
    ].map((id) => ({
      id,
      name: id,
      state: feedsReady ? "ready" : "stale",
      lastSuccess: new Date().toISOString(),
      lastAttempt: new Date().toISOString(),
      nextAttempt: null,
      message: "Browser test fixture",
    })),
    bridge: selected?.bridge?.position ?? null,
    generatedAt: new Date().toISOString(),
    attribution: [],
    settingsAvailable: true,
  };
}
try {
  if (process.env.COMMUTE_UI_ONLY !== "1") {
    // Verify the module's real DB-backed read endpoint before switching to UI fixtures.
    const real = await page.request.get(`${apiBase}/api/commute/workspace`);
    assert.equal(
      real.status(),
      200,
      "Module database must initialize successfully",
    );
    const actual = await real.json();
    assert.equal(
      "googleMapsEmbedKey" in actual,
      false,
      "Workspace endpoint must not expose the Maps key",
    );
  }
  await page.route("**/assets/vessel-ports.private.json", (r) =>
    r.fulfill({
      json: [
        ["XXAAA", "Synthetic Port", "Synthetic Country", 0.01, 0.02, ""],
        ["XXBBB", "Synthetic Second Port", "Synthetic Country", 0.02, 0.03, ""],
      ],
    }),
  );
  await page.route("**/api/commute**", async (intercepted) => {
    const request = intercepted.request();
    const path = new URL(request.url()).pathname;
    const data = ["PUT", "POST"].includes(request.method())
      ? request.postDataJSON()
      : null;
    if (path.endsWith("/workspace"))
      return intercepted.fulfill({ json: workspace });
    if (path.endsWith("/route/reverse")) {
      const selected = workspace.routes.find((r) => r.id === data.routeId);
      [selected.origin, selected.destination] = [
        selected.destination,
        selected.origin,
      ];
      selected.points.reverse();
      selected.direction =
        selected.direction === "Eastbound" ? "Westbound" : "Eastbound";
      selected.name = `${selected.origin} → ${selected.destination}`;
      return intercepted.fulfill({ json: workspace });
    }
    if (path.endsWith("/route") && request.method() === "PUT") {
      data.id =
        data.id === "00000000-0000-0000-0000-000000000000"
          ? "33333333-3333-3333-3333-333333333333"
          : data.id;
      workspace.routes = workspace.routes
        .filter((r) => r.id !== data.id)
        .concat(data);
      workspace.activeRouteId = data.id;
      workspace.dashboards = workspace.dashboards.map((profile) =>
        profile.id === workspace.activeDashboardId &&
        !profile.routeIds.includes(data.id)
          ? { ...profile, routeIds: [...profile.routeIds, data.id] }
          : profile,
      );
      return intercepted.fulfill({ json: data });
    }
    if (path.endsWith("/dashboards") && request.method() === "PUT") {
      data.id =
        data.id === "00000000-0000-0000-0000-000000000000"
          ? profileId
          : data.id;
      workspace.dashboards = workspace.dashboards
        .filter((p) => p.id !== data.id)
        .concat(data);
      workspace.activeDashboardId = data.id;
      workspace.activeRouteId = data.routeIds[0] ?? null;
      return intercepted.fulfill({ json: workspace });
    }
    if (path.endsWith("/active")) {
      workspace.activeRouteId = data.routeId;
      workspace.activeDashboardId = data.dashboardId;
      return intercepted.fulfill({ json: workspace });
    }
    if (path.endsWith("/maps/key")) {
      mapsKey = data.key;
      workspace.googleMapsConfigured = !!mapsKey;
      return intercepted.fulfill({ json: workspace });
    }
    if (path.endsWith("/maps"))
      return intercepted.fulfill({
        json: {
          directionsUrl:
            "https://www.google.com/maps/dir/?api=1&origin=0%2C0&destination=0.01%2C0.01&travelmode=driving",
          embedUrl: mapsKey
            ? `https://www.google.com/maps/embed/v1/directions?key=${mapsKey}&origin=0%2C0&destination=0.01%2C0.01`
            : null,
        },
      });
    if (path.includes("/dashboards/") && request.method() === "DELETE") {
      workspace.dashboards = [];
      workspace.activeDashboardId = null;
      return intercepted.fulfill({ json: workspace });
    }
    return intercepted.fulfill({ json: snapshot() });
  });
  await page.route("**/test-camera.svg*", (r) =>
    r.fulfill({
      contentType: "image/svg+xml",
      body: '<svg xmlns="http://www.w3.org/2000/svg" width="640" height="360"><rect width="640" height="360" fill="#172a3a"/><text x="60" y="180" fill="white" font-size="26">Synthetic camera view</text></svg>',
    }),
  );
  await page.route("**/missing-camera.jpg*", (r) =>
    r.fulfill({ status: 404, body: "" }),
  );
  await page.route("https://tile.openstreetmap.org/**", (r) =>
    r.fulfill({
      contentType: "image/png",
      body: Buffer.from(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=",
        "base64",
      ),
    }),
  );
  let googleRequests = 0;
  await page.route("https://www.google.com/maps/embed/**", (r) => {
    googleRequests++;
    return r.fulfill({
      contentType: "text/html",
      body: "<p>Synthetic Google map</p>",
    });
  });
  await page.goto(`${base}/commute`);
  await page.getByRole("tab", { name: "Map", exact: true }).click();
  const overview = page.locator(".corridor-map");
  await overview.waitFor();
  const cameraMarker = overview.getByRole("button", {
    name: "Camera: Synthetic camera",
    exact: true,
  });
  await cameraMarker.waitFor();
  assert.equal(
    await overview.locator('svg path[fill="none"]').count(),
    1,
    "Only the saved trip route should be drawn",
  );
  const dryMarker = overview
    .getByRole("button", { name: "Synthetic dry road", exact: true })
    .locator("span");
  assert.equal(
    await dryMarker.evaluate((el) => getComputedStyle(el).backgroundColor),
    "rgb(100, 116, 139)",
  );
  assert(
    !(
      await page
        .locator(".issue-row")
        .filter({ hasText: "Synthetic dry road" })
        .innerText()
    ).includes("freshness unconfirmed"),
  );
  const redMarker = overview
    .getByRole("button", { name: "Synthetic full closure", exact: true })
    .locator("span");
  const bridgeMarker = overview.getByRole("button", {
    name: "Synthetic lift bridge",
    exact: true,
  });
  assert.equal(
    await redMarker.evaluate((el) => getComputedStyle(el).backgroundColor),
    "rgb(220, 38, 38)",
  );
  assert.equal(
    await bridgeMarker
      .locator("span")
      .evaluate((el) => getComputedStyle(el).backgroundColor),
    "rgb(180, 83, 9)",
  );
  await bridgeMarker.click();
  await page.locator(".map-detail-dialog[open]").waitFor();
  assert(
    (await page.locator(".map-detail-dialog").innerText()).includes(
      "Synthetic possible lift",
    ),
  );
  assert.equal(
    await page
      .locator(".map-detail-dialog")
      .evaluate((el) => getComputedStyle(el).backgroundColor),
    "rgb(255, 255, 255)",
  );
  await page.keyboard.press("Escape");
  feedsReady = false;
  await page
    .getByRole("button", { name: "Refresh dashboard", exact: true })
    .click();
  await page.waitForFunction(
    () =>
      getComputedStyle(
        document.querySelector('[aria-label="Synthetic full closure"] span'),
      ).backgroundColor === "rgb(100, 116, 139)",
  );
  assert.equal(
    await bridgeMarker
      .locator("span")
      .evaluate((el) => getComputedStyle(el).backgroundColor),
    "rgb(15, 23, 42)",
  );
  feedsReady = true;
  await page
    .getByRole("button", { name: "Refresh dashboard", exact: true })
    .click();
  await page.waitForFunction(
    () =>
      getComputedStyle(
        document.querySelector('[aria-label="Synthetic full closure"] span'),
      ).backgroundColor === "rgb(220, 38, 38)",
  );
  const initialPosition = await cameraMarker.getAttribute("style");
  assert.equal(
    await overview.getByText("OpenStreetMap", { exact: true }).count(),
    1,
  );
  await overview.getByRole("button", { name: "Zoom in", exact: true }).click();
  await page.waitForTimeout(400);
  assert.notEqual(await cameraMarker.getAttribute("style"), initialPosition);
  const pane = overview.locator(".leaflet-map-pane");
  await overview.focus();
  const beforePan = await pane.getAttribute("style");
  await page.keyboard.press("ArrowRight");
  await page.waitForTimeout(400);
  assert.notEqual(await pane.getAttribute("style"), beforePan);
  const beforeRefresh = await pane.getAttribute("style");
  await page
    .getByRole("button", { name: "Refresh dashboard", exact: true })
    .click();
  await page
    .getByRole("button", { name: "Refresh dashboard", exact: true })
    .waitFor();
  assert.equal(
    await pane.getAttribute("style"),
    beforeRefresh,
    "Feed refresh must preserve the map view",
  );
  const beforeDrag = await pane.getAttribute("style");
  await overview.scrollIntoViewIfNeeded();
  const bounds = await overview.boundingBox();
  await page.mouse.move(
    bounds.x + bounds.width * 0.6,
    bounds.y + bounds.height * 0.5,
  );
  await page.mouse.down();
  await page.mouse.move(
    bounds.x + bounds.width * 0.5,
    bounds.y + bounds.height * 0.5,
    { steps: 5 },
  );
  await page.mouse.up();
  await page.waitForTimeout(400);
  assert.notEqual(await pane.getAttribute("style"), beforeDrag);
  assert.equal(
    await page.locator("dialog[open]").count(),
    0,
    "Dragging should not open marker details",
  );
  await page.getByRole("button", { name: "Reset view", exact: true }).click();
  await page.waitForTimeout(400);
  assert.equal(await cameraMarker.getAttribute("style"), initialPosition);
  await page
    .getByRole("button", { name: "Vessel: Synthetic ship", exact: true })
    .click();
  const detail = page.locator(".map-detail-dialog");
  await detail.waitFor({ state: "visible" });
  assert((await detail.innerText()).includes("7.2 kn"));
  assert((await detail.innerText()).includes("123456789"));
  assert((await detail.innerText()).includes("Reported destination: XXAAA"));
  assert(
    (await detail.innerText()).includes(
      "Destination port: Synthetic Port, Synthetic Country",
    ),
  );
  await detail
    .getByRole("button", { name: "View ship on map", exact: true })
    .click();
  await detail.waitFor({ state: "hidden" });
  const courseLine = overview.locator(".vessel-destination-connection");
  await courseLine.waitFor();
  await overview
    .getByText("Destination port: Synthetic Port, Synthetic Country", {
      exact: true,
    })
    .last()
    .waitFor();
  const selectedShip = page.getByRole("region", {
    name: "Selected ship",
    exact: true,
  });
  // The labelled section is a region in the accessibility tree.
  assert(
    (await selectedShip.innerText()).includes("Reported destination: XXAAA"),
  );
  assert((await selectedShip.innerText()).includes("not the sailing path"));
  const beforeCourseRefresh = await pane.getAttribute("style");
  const originalCourse = await courseLine.getAttribute("d");
  shipOverrides = { destination: "XXBBB" };
  await page
    .getByRole("button", { name: "Refresh dashboard", exact: true })
    .click();
  await page.waitForFunction(
    (previous) =>
      document
        .querySelector(".vessel-destination-connection")
        ?.getAttribute("d") !== previous,
    originalCourse,
  );
  await overview
    .getByText("Destination port: Synthetic Second Port, Synthetic Country", {
      exact: true,
    })
    .last()
    .waitFor();
  assert.equal(
    await pane.getAttribute("style"),
    beforeCourseRefresh,
    "Refreshing a selected ship must preserve map focus",
  );
  shipOverrides = {
    seenAt: new Date(Date.now() - 360000).toISOString(),
    destination: "",
  };
  await page
    .getByRole("button", { name: "Refresh dashboard", exact: true })
    .click();
  await selectedShip
    .getByText("No destination port was reported by AIS.", {
      exact: true,
    })
    .waitFor();
  assert.equal(await courseLine.count(), 0);
  assert((await selectedShip.innerText()).includes("Not reported by AIS"));
  shipOverrides = {};
  await page
    .getByRole("button", { name: "Refresh dashboard", exact: true })
    .click();
  await courseLine.waitFor();
  feedsReady = false;
  await page
    .getByRole("button", { name: "Refresh dashboard", exact: true })
    .click();
  await selectedShip
    .getByText(
      "Destination connection unavailable while the marine feed is stale or offline.",
      { exact: true },
    )
    .waitFor();
  assert.equal(await courseLine.count(), 0);
  feedsReady = true;
  await page
    .getByRole("button", { name: "Refresh dashboard", exact: true })
    .click();
  await courseLine.waitFor();
  await selectedShip
    .getByRole("button", { name: "Clear ship selection", exact: true })
    .click();
  assert.equal(await courseLine.count(), 0);
  await page.getByRole("button", { name: "Reset view", exact: true }).click();
  await page
    .getByRole("button", { name: "Vessel: Synthetic ship", exact: true })
    .click();
  await detail.waitFor({ state: "visible" });

  await page.keyboard.press("Escape");
  await detail.waitFor({ state: "hidden" });
  const reportMarker = overview.getByRole("button", {
    name: "Synthetic lane works",
    exact: true,
  });
  await reportMarker.click();
  await detail.waitFor({ state: "visible" });
  assert((await detail.innerText()).includes("Right lane"));
  assert((await detail.innerText()).includes("Overnight"));
  await page.getByRole("button", { name: "Close map details" }).click();
  await reportMarker.press("Enter");
  await detail.waitFor({ state: "visible", timeout: 3000 });
  await page.getByRole("button", { name: "Close map details" }).click();

  await page.getByRole("button", { name: "Reverse trip", exact: true }).click();
  await page
    .getByRole("heading", { name: "End → Start", exact: true })
    .waitFor();
  assert.equal(workspace.routes[0].direction, "Westbound");
  assert.deepEqual(workspace.routes[0].points[0], {
    latitude: 0.01,
    longitude: 0.01,
  });
  await page.reload();
  await page
    .getByRole("heading", { name: "End → Start", exact: true })
    .waitFor();
  await page.getByRole("button", { name: "Reverse trip", exact: true }).click();
  await page
    .getByRole("heading", { name: "Start → End", exact: true })
    .waitFor();
  assert.equal(workspace.routes[0].direction, "Eastbound");
  await page.getByRole("tab", { name: "My dashboard", exact: true }).click();
  assert.equal(await page.locator(".feed-card").count(), 6);
  await page
    .getByRole("button", {
      name: "Enlarge camera Synthetic camera",
      exact: true,
    })
    .click();
  assert(
    await page
      .locator("dialog:not(.map-detail-dialog)")
      .evaluate((d) => d.open),
  );
  await page
    .locator("dialog:not(.map-detail-dialog)")
    .getByRole("button", { name: "Alternate view", exact: true })
    .click();
  await page
    .getByText("The camera image could not be loaded.", { exact: true })
    .waitFor();
  await page.keyboard.press("Escape");
  assert.equal(
    await page
      .locator("dialog:not(.map-detail-dialog)")
      .evaluate((d) => d.open),
    false,
  );
  await page
    .getByRole("tab", { name: "Build my dashboard", exact: true })
    .click();
  assert.equal(await page.locator(".workspace-switcher").count(), 0);
  await page
    .getByRole("button", { name: "New dashboard", exact: true })
    .click();
  await page
    .getByLabel("Dashboard name", { exact: true })
    .fill("Custom dashboard");
  await page
    .getByRole("checkbox", {
      name: "Show only pinned views in this dashboard",
      exact: true,
    })
    .check();
  await page.getByRole("checkbox", { name: "Main view", exact: true }).check();
  await page
    .getByRole("checkbox", { name: "Marine traffic", exact: true })
    .uncheck();
  await page
    .getByRole("button", { name: "Move Camera views up", exact: true })
    .click();
  await page
    .getByRole("button", { name: "Save dashboard", exact: true })
    .click();
  await page
    .getByText("Dashboard saved to the database.", { exact: true })
    .waitFor();
  assert.equal(workspace.dashboards[0].favoritesOnly, true);
  assert.equal(workspace.dashboards[0].favoriteViews.length, 1);
  assert.equal(await page.locator(".camera-card").count(), 1);
  assert.equal(
    await page.locator('[aria-labelledby="marine-title"]').count(),
    0,
  );
  await page.reload();
  await page
    .getByText("Showing pinned camera views on the selected route.", {
      exact: true,
    })
    .waitFor();
  await page
    .getByRole("tab", { name: "Build my dashboard", exact: true })
    .click();
  await page.getByRole("button", { name: "Add route", exact: true }).click();
  await page
    .getByLabel("Route name", { exact: true })
    .fill("Second synthetic route");
  await page.getByLabel("Road names / aliases", { exact: true }).fill("A1");
  await page
    .getByLabel("Regional alert names", { exact: true })
    .pressSequentially("Synthetic region, Other synthetic region");
  await page.getByLabel("Origin", { exact: true }).fill("New start");
  await page.getByLabel("Destination", { exact: true }).fill("New end");
  await page.getByRole("textbox", { name: /Waypoints/ }).fill("invalid");
  await page.getByRole("button", { name: "Save route", exact: true }).click();
  await page
    .getByText("Enter one latitude, longitude pair per line.", { exact: true })
    .waitFor();
  await page
    .getByRole("textbox", { name: /Waypoints/ })
    .fill("0, 0\n0.01, 0.01");
  await page.getByRole("button", { name: "Save route", exact: true }).click();
  await page
    .getByRole("heading", { name: "Second synthetic route", exact: true })
    .waitFor();
  assert.equal(workspace.routes.length, 2);
  assert.deepEqual(workspace.routes.at(-1).alertRegions, ["Synthetic region", "Other synthetic region"]);
  await page
    .getByRole("tab", { name: "Build my dashboard", exact: true })
    .click();
  await page
    .getByLabel("Maps Embed API key", { exact: true })
    .fill("browser-test-key");
  await page
    .getByRole("button", { name: "Save Maps key", exact: true })
    .click();
  await page
    .getByText("An Embed API key is configured.", { exact: true })
    .waitFor();
  assert.equal(
    googleRequests,
    0,
    "Opening dashboard must not load Google without a click",
  );
  await page.getByRole("tab", { name: "Map", exact: true }).click();
  await page.getByText("Google route options", { exact: true }).click();
  await page
    .getByRole("button", { name: "Load Google route map", exact: true })
    .click();
  await page.locator(".google-route-map").waitFor();
  const iframe = await page.locator(".google-route-map").getAttribute("src");
  assert(iframe.startsWith("https://www.google.com/maps/embed/v1/directions?"));
  await page
    .getByRole("button", { name: "Hide Google map", exact: true })
    .click();
  await page.setViewportSize({ width: 390, height: 844 });
  await page.screenshot({
    path: "/tmp/commute-builder-mobile.png",
    fullPage: true,
  });
  assert.equal(
    await page.evaluate(
      () => document.documentElement.scrollWidth > window.innerWidth,
    ),
    false,
  );
  await page
    .getByRole("tab", { name: "Build my dashboard", exact: true })
    .click();
  await page
    .getByRole("button", {
      name: "Delete dashboard Custom dashboard",
      exact: true,
    })
    .click();
  await page.getByRole("button", { name: "Keep", exact: true }).click();
  assert.equal(
    workspace.dashboards.length,
    1,
    "Canceling deletion must retain the dashboard",
  );
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.waitForFunction(
    () => document.querySelector(".sidebar").getBoundingClientRect().width < 80,
  );
  await page.getByRole("heading", { name: "Commute Monitor", exact: true }).scrollIntoViewIfNeeded();
  await page.screenshot({ path: "/tmp/commute-builder-desktop.png" });
  assert.equal(errors.length, 0, errors.join("\n"));
  console.log(
    `PASS: ${process.env.COMMUTE_UI_ONLY === "1" ? "UI fixtures only" : "DB-backed endpoint"}, custom dashboard/favorites/order, save/reload, new route validation, camera dialog, map tab, a single route line, red/amber alerts, stale highlight suppression, readable dialogs, zoom/pan and marker details, persistent trip reversal, ship destination port connections and stale suppression, click-to-load Google Maps, deletion cancellation and mobile layout.`,
  );
} catch (error) {
  await page.screenshot({ path: "/tmp/commute-smoke-failure.png", fullPage: true });
  console.error("Browser errors:", errors);
  throw error;
} finally {
  await browser.close();
}
