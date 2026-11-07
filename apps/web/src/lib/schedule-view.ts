export function localDay(instant: string, zone: string) {
  const parts = new Intl.DateTimeFormat("en-CA", {
    timeZone: zone,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).formatToParts(new Date(instant));
  const part = (type: string) => parts.find((p) => p.type === type)!.value;
  return `${part("year")}-${part("month")}-${part("day")}`;
}
export function addDays(day: string, amount: number) {
  const value = new Date(day + "T12:00:00Z");
  value.setUTCDate(value.getUTCDate() + amount);
  return value.toISOString().slice(0, 10);
}
export function dayLabel(day: string) {
  return new Intl.DateTimeFormat("en", {
    timeZone: "UTC",
    weekday: "short",
    month: "short",
    day: "numeric",
  }).format(new Date(day + "T12:00:00Z"));
}
export function timeRange(start: string, end: string, zone: string) {
  const format = new Intl.DateTimeFormat("en", {
    timeZone: zone,
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  });
  const a = localDay(start, zone);
  const b = localDay(end, zone);
  return `${format.format(new Date(start))}–${format.format(new Date(end))}${a === b ? "" : ` (${dayLabel(b)})`}`;
}
export function occursOn(
  start: string,
  end: string,
  day: string,
  zone: string,
) {
  return (
    localDay(start, zone) <= day &&
    localDay(new Date(new Date(end).getTime() - 1).toISOString(), zone) >= day
  );
}
