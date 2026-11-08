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
  let displayedIdentity: string | null | undefined;
  let sessionGeneration = 0;
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
  async function identityToken() {
    const csrf = await get(
      "/api/v1/auth/csrf",
      z.object({ token: z.string().min(1), userId: z.guid().nullable() }),
    );
    if (displayedIdentity !== undefined && csrf.userId !== displayedIdentity)
      throw new ApiError(
        401,
        "Your session changed in another tab. Sign in again before saving.",
      );
    return csrf;
  }
  return {
    get,
    async assertIdentity() {
      await identityToken();
    },
    async refreshSession() {
      const current = await get("/api/v1/auth/session", sessionSchema);
      if (current.id !== displayedIdentity)
        throw new ApiError(
          401,
          "Your session changed in another tab. Sign in again before saving.",
        );
      return current;
    },
    async session() {
      const generation = ++sessionGeneration;
      try {
        const session = await get("/api/v1/auth/session", sessionSchema);
        if (generation === sessionGeneration) displayedIdentity = session.id;
        return session;
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) {
          if (generation === sessionGeneration) displayedIdentity = null;
          return null;
        }
        throw error;
      }
    },
    async save(
      path: string,
      body?: unknown,
      method = "POST",
      generation?: string | null,
    ) {
      const csrf =
        path === "/api/v1/auth/login"
          ? await get(
              "/api/v1/auth/csrf",
              z.object({
                token: z.string().min(1),
                userId: z.guid().nullable(),
              }),
            )
          : await identityToken();
      if (
        path !== "/api/v1/auth/login" &&
        displayedIdentity !== undefined &&
        csrf.userId !== displayedIdentity
      )
        throw new ApiError(
          401,
          "Your session changed in another tab. Sign in again before saving.",
        );
      try {
        return await request(path, {
          method,
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": csrf.token,
            ...(generation ? { "X-Workspace-Generation": generation } : {}),
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
