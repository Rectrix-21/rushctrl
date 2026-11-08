import {
  HubConnectionBuilder,
  HttpTransportType,
  LogLevel,
} from "@microsoft/signalr";
import { z } from "zod";
import { ApiError } from "./workspace-api.ts";

const changeSchema = z.object({
  organizationId: z.guid(),
  generation: z.guid().nullable(),
});
type Connection = {
  start(): Promise<void>;
  stop(): Promise<void>;
  invoke(method: string, id: string): Promise<unknown>;
  on(event: string, callback: (value: unknown) => void): void;
  onreconnecting(callback: () => void): void;
  onreconnected(callback: () => Promise<void>): void;
  onclose(callback: () => void): void;
};
export function startWorkspaceUpdates(options: {
  org: string;
  generation: string | null;
  verifyIdentity: () => Promise<void>;
  onChange: (reset: boolean) => void;
  onStatus: (status: string) => void;
  onExpired: () => void;
  connection?: Connection;
}) {
  const connection =
    options.connection ??
    new HubConnectionBuilder()
      .withUrl("/hubs/workspace", { transport: HttpTransportType.LongPolling })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(LogLevel.None)
      .build();
  let stopped = false,
    editing = false,
    pending = false,
    resetPending = false;
  let timer: ReturnType<typeof setTimeout> | undefined;
  function stop() {
    stopped = true;
    clearTimeout(timer);
    void connection.stop();
  }
  async function failure(error: unknown) {
    if (stopped) return;
    if (error instanceof ApiError && error.status === 401) {
      stop();
      options.onExpired();
    } else options.onStatus("Live updates offline · use Refresh");
  }
  function flush() {
    clearTimeout(timer);
    timer = setTimeout(() => {
      if (stopped || !pending || (editing && !resetPending)) return;
      const reset = resetPending;
      pending = false;
      resetPending = false;
      options.onChange(reset);
    }, 60);
  }
  async function joined() {
    try {
      await options.verifyIdentity();
      if (stopped) return;
      await connection.invoke("JoinWorkspace", options.org);
      await options.verifyIdentity();
      if (stopped) return;
      options.onStatus("Live updates connected");
      pending = true;
      flush();
    } catch (error) {
      await failure(error);
    }
  }
  connection.on("WorkspaceChanged", (value) => {
    const parsed = changeSchema.safeParse(value);
    if (
      stopped ||
      !parsed.success ||
      parsed.data.organizationId !== options.org
    )
      return;
    void options
      .verifyIdentity()
      .then(() => {
        if (stopped) return;
        pending = true;
        resetPending ||= parsed.data.generation !== options.generation;
        flush();
      })
      .catch(failure);
  });
  connection.onreconnecting(() => {
    if (!stopped) options.onStatus("Live updates reconnecting");
  });
  connection.onreconnected(joined);
  connection.onclose(() => {
    if (!stopped) options.onStatus("Live updates offline · use Refresh");
  });
  options.onStatus("Connecting live updates");
  void (async () => {
    try {
      await options.verifyIdentity();
      if (stopped) return;
      await connection.start();
      if (stopped) {
        await connection.stop();
        return;
      }
      await joined();
    } catch (error) {
      await failure(error);
    }
  })();
  return {
    stop,
    setEditing(value: boolean) {
      editing = value;
      if (!value) flush();
    },
  };
}
