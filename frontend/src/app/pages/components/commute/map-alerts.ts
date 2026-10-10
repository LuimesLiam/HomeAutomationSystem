import { CommuteSnapshot, RoadItem } from "./commute.models";

export type MapSeverity = "danger" | "warning" | "neutral";

export function reportFeedFresh(
  item: RoadItem,
  data: CommuteSnapshot | null,
  offline = false,
): boolean {
  const feedId =
    item.kind === "incident"
      ? "incidents"
      : item.kind === "construction"
        ? "construction"
        : item.kind === "alert"
          ? "alerts"
          : "conditions";
  return (
    !offline && data?.feeds.find((f) => f.id === feedId)?.state === "ready"
  );
}

export function reportSeverity(
  item: RoadItem,
  data: CommuteSnapshot | null,
  offline = false,
): MapSeverity {
  if (
    !reportFeedFresh(item, data, offline) ||
    (item.kind === "condition" && !item.advisory)
  )
    return "neutral";
  if (item.startsAt && new Date(item.startsAt).getTime() > Date.now())
    return "warning";
  return item.fullClosure || item.kind === "incident" ? "danger" : "warning";
}

export function liftSeverity(level: string): MapSeverity {
  return level === "IMMINENT" || level === "LIKELY"
    ? "danger"
    : level === "POSSIBLE"
      ? "warning"
      : "neutral";
}

export const severityColors: Record<MapSeverity, string> = {
  danger: "#dc2626",
  warning: "#b45309",
  neutral: "#64748b",
};
