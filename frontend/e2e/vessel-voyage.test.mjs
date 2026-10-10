import assert from "node:assert/strict";
import test from "node:test";
import {
  configureDestinationPorts,
  destinationPort,
  reportedDestination,
  vesselVoyage,
} from "../src/app/pages/components/commute/vessel-voyage.ts";
const ports = [
  ["XXAAA", "Synthetic Port", "Synthetic Country", 0.01, 0.02, ""],
  ["XXBBB", "Synthetic Second Port", "Synthetic Country", 0.02, 0.03, ""],
  ["XXCCC", "Synthetic Port", "Other Synthetic Country", 0.03, 0.04, ""],
];
configureDestinationPorts(ports);
const now = Date.parse("2026-10-09T18:00:00Z");
const data = { feeds: [{ id: "marine", state: "ready" }] };
const vessel = {
  position: { latitude: 0.002, longitude: 0.003 },
  destination: "XXAAA",
  speedKnots: null,
  course: null,
  navigationStatus: 5,
  seenAt: new Date(now).toISOString(),
  positionFreshnessKnown: true,
};
test("reported destination retains text and missing data has an explicit fallback", () => {
  assert.equal(reportedDestination(vessel), "XXAAA");
  assert.equal(reportedDestination({ destination: "XX AAA@@@" }), "XX AAA");
  for (const destination of ["", "  ", "@@@", "UNKNOWN", "N/A", "???"])
    assert.equal(reportedDestination({ destination }), "Not reported by AIS");
});
test("codes and qualified names resolve a port without guessing ambiguous names", () => {
  const syntheticPort = destinationPort("XX AAA");
  assert.equal(syntheticPort.name, "Synthetic Port");
  assert.equal(syntheticPort.country, "Synthetic Country");
  assert.deepEqual(destinationPort("xx-aaa"), syntheticPort);
  assert.deepEqual(
    destinationPort("Synthetic Port, Synthetic Country"),
    syntheticPort,
  );
  assert.equal(destinationPort("Synthetic Port"), null);
  assert.equal(destinationPort("XXAAA > XXBBB"), null);
  assert.equal(destinationPort("unrecognized destination"), null);
});
test("connection reaches the destination port regardless of speed, course or berth status", () => {
  const voyage = vesselVoyage(vessel, data, false, now);
  assert.deepEqual(voyage.connection.points, [
    vessel.position,
    voyage.port.position,
  ]);
  assert.equal(voyage.connection.points.at(-1).latitude, 0.01);
  assert.equal(voyage.connection.points.at(-1).longitude, 0.02);
  assert.deepEqual(
    vesselVoyage({ ...vessel, speedKnots: 20, course: 90 }, data, false, now)
      .connection,
    voyage.connection,
  );
  const changed = vesselVoyage(
    { ...vessel, destination: "XXBBB" },
    data,
    false,
    now,
  );
  assert.equal(changed.port.name, "Synthetic Second Port");
  assert.notDeepEqual(changed.connection, voyage.connection);
});
test("missing, ambiguous or unmatched destinations never produce a guessed connection", () => {
  for (const destination of [
    "",
    "Synthetic Port",
    "Unknown port",
    "XXAAA > XXBBB",
  ]) {
    const voyage = vesselVoyage({ ...vessel, destination }, data, false, now);
    assert.equal(voyage.connection, null);
    assert(voyage.unavailableReason);
  }
});
test("stale and unusable AIS positions suppress the connection but retain the resolved port", () => {
  for (const change of [
    { positionFreshnessKnown: false },
    { seenAt: null },
    { seenAt: "invalid" },
    { seenAt: new Date(now - 300001).toISOString() },
    { seenAt: new Date(now + 60001).toISOString() },
    { position: { latitude: 91, longitude: 0 } },
    { position: { latitude: 0, longitude: NaN } },
  ]) {
    const voyage = vesselVoyage({ ...vessel, ...change }, data, false, now);
    assert.equal(voyage.connection, null);
    assert.equal(voyage.port.code, "XXAAA");
    assert(voyage.unavailableReason);
  }
  assert.equal(vesselVoyage(vessel, data, true, now).connection, null);
  assert.equal(
    vesselVoyage(
      vessel,
      { feeds: [{ id: "marine", state: "stale" }] },
      false,
      now,
    ).connection,
    null,
  );
  assert.equal(vesselVoyage(null, data, false, now).connection, null);
});

test("catalogue is optional, replacement clears old ports, and malformed rows are ignored", () => {
  for (const catalogue of [
    null,
    {},
    [],
    [null, [], ["code", 123, "country", 0, 0, ""]],
    [["XXAAA", "Synthetic Port", "Synthetic Country", 91, 0, ""]],
  ]) {
    configureDestinationPorts(catalogue);
    assert.equal(destinationPort("XXAAA"), null);
    assert.equal(vesselVoyage(vessel, data, false, now).connection, null);
  }
  configureDestinationPorts(ports);
  assert.equal(destinationPort("XXAAA").name, "Synthetic Port");
});
