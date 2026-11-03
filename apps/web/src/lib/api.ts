export type ConnectionState =
  | "ready"
  | "api-unavailable"
  | "database-unavailable";

export function createConnectionCheck(
  publish: (state: ConnectionState) => void,
  fetcher: typeof fetch = fetch,
) {
  let generation = 0;
  return {
    async refresh() {
      const request = ++generation;
      const state = await loadConnectionState(fetcher);
      if (request === generation) publish(state);
    },
    cancel() {
      generation++;
    },
  };
}

export async function loadConnectionState(
  fetcher: typeof fetch = fetch,
): Promise<ConnectionState> {
  const options: RequestInit = {
    cache: "no-store",
    signal: AbortSignal.timeout(4000),
  };
  try {
    const response = await fetcher("/api/v1/system", options);
    if (!response.ok) return "api-unavailable";
    const metadata: unknown = await response.json();
    if (
      !metadata ||
      typeof metadata !== "object" ||
      !("name" in metadata) ||
      metadata.name !== "RUSHCTRL" ||
      !("version" in metadata) ||
      typeof metadata.version !== "string" ||
      !("milestone" in metadata) ||
      metadata.milestone !== 0
    )
      return "api-unavailable";
  } catch {
    return "api-unavailable";
  }
  try {
    const response = await fetcher("/health/ready", options);
    if (!response.ok) return "database-unavailable";
    const health: unknown = await response.json();
    return health &&
      typeof health === "object" &&
      "status" in health &&
      health.status === "healthy"
      ? "ready"
      : "database-unavailable";
  } catch {
    return "database-unavailable";
  }
}
