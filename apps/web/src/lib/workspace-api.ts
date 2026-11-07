import { z } from "zod";
import {
  sessionSchema,
  workspaceSchema,
  shiftsSchema,
  availabilitySchema,
  requestsSchema,
  gapsSchema,
  requirementsSchema,
} from "./contracts.ts";

export class ApiError extends Error {
  status: number;
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}
export function createWorkspaceClient(fetcher: typeof fetch = fetch) {
  async function request(path: string, options: RequestInit = {}) {
    let response: Response;
    try {
      response = await fetcher(path, {
        ...options,
        credentials: "same-origin",
        cache: "no-store",
        signal: AbortSignal.timeout(10000),
      });
    } catch {
      throw new ApiError(
        0,
        "Cannot reach RUSHCTRL. Check the connection and try again.",
      );
    }
    if (!response.ok) {
      const problem = await response.json().catch(() => null);
      const message =
        response.status >= 500
          ? "RUSHCTRL could not complete that request. Try again shortly."
          : typeof problem?.title === "string"
            ? problem.title
            : "The request could not be completed.";
      throw new ApiError(response.status, message);
    }
    return response;
  }
  async function get<T>(path: string, schema: z.ZodType<T>): Promise<T> {
    const response = await request(path);
    const parsed = schema.safeParse(await response.json().catch(() => null));
    if (!parsed.success)
      throw new ApiError(
        502,
        "RUSHCTRL sent an unexpected response. Refresh and try again.",
      );
    return parsed.data;
  }
  return {
    get,
    async session() {
      try {
        return await get("/api/v1/auth/session", sessionSchema);
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) return null;
        throw error;
      }
    },
    async save(path: string, body?: unknown, method = "POST") {
      const csrf = await get(
        "/api/v1/auth/csrf",
        z.object({ token: z.string().min(1) }),
      );
      try {
        await request(path, {
          method,
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": csrf.token,
          },
          body: body === undefined ? undefined : JSON.stringify(body),
        });
      } catch (error) {
        if (error instanceof ApiError && error.status === 0)
          throw new ApiError(
            0,
            "The save outcome is unknown. Refresh before trying again.",
          );
        throw error;
      }
    },
  };
}
export type WorkspaceClient = ReturnType<typeof createWorkspaceClient>;

export async function loadSchedule(
  client: WorkspaceClient,
  organizationId: string,
  locationId: string,
  week: string,
  manager: boolean,
) {
  const base = `/api/v1/organizations/${organizationId}/`;
  const query = `?locationId=${encodeURIComponent(locationId)}&week=${encodeURIComponent(week)}`;
  const [shifts, availability, requests, gaps, requirements] =
    await Promise.all([
      client.get(base + "shifts" + query, shiftsSchema),
      client.get(base + "availability" + query, availabilitySchema),
      client.get(base + "coverage-requests" + query, requestsSchema),
      manager
        ? client.get(base + "coverage" + query, gapsSchema)
        : Promise.resolve([]),
      manager
        ? client.get(base + "staffing-requirements" + query, requirementsSchema)
        : Promise.resolve([]),
    ]);
  return { shifts, availability, requests, gaps, requirements };
}
export type ScheduleData = Awaited<ReturnType<typeof loadSchedule>>;
export { workspaceSchema };
