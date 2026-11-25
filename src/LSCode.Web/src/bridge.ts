export type DocumentInfo = {
  id: string;
  path: string | null;
  name: string;
  text: string;
  encoding: string;
  bom: boolean;
  newline: string;
  version: number;
};
export type Target = { id: string; server: string; database: string };
export type Project = {
  id: string;
  name: string;
  server: string;
  database: string;
  sourcePath: string;
  code: string;
  title: string;
  programPath: string;
  active: boolean;
};
export type Snippet = {
  key: string;
  category: string;
  body: string;
  description: string;
  position: number;
  param: string;
};
export type Table = { columns: string[]; rows: (string | null)[][] };
export type QueryResult = {
  tables: Table[];
  messages: string[];
  durationMs: number;
  truncated: boolean;
};
export type Settings = {
  theme: string;
  accentColor?: string | null;
  explorerFontSize?: number;
  editorFontSize?: number;
  explorerWidth?: number;
  workspace: string | null;
  server: string;
  database: string;
  integratedSecurity: boolean;
  sqlUser: string;
  trustCertificate: boolean;
  crystalPath: string;
  profiler18Path: string;
  standardCatalogUser?: string;
  shortcuts: Record<string, string[]>;
  projectLastAccess?: Record<string, string>;
  activityCommands?: string[] | null;
  [key: string]: unknown;
};
export type Bootstrap = {
  settings: Settings;
  projects: Project[];
  snippets: Snippet[];
  version: string;
};
type Native = {
  postMessage: (data: unknown) => void;
  postMessageWithAdditionalObjects?: (data: unknown, objects: File[]) => void;
  addEventListener: (type: "message", fn: (e: MessageEvent) => void) => void;
};
declare global {
  interface Window {
    __LSCODE_SMOKE__?: boolean;
    chrome?: { webview?: Native };
    lscodeSmoke?: () => Promise<void>;
  }
}
const pending = new Map<
  string,
  { resolve: (value: unknown) => void; reject: (reason: Error) => void }
>();
export type NativeEvent = {
  type: string;
  paths?: string[];
  requestId?: string;
  hits?: { path: string; line: number; column: number; preview: string }[];
};
const listeners = new Set<(event: NativeEvent) => void>();
export const native = Boolean(window.chrome?.webview);
window.chrome?.webview?.addEventListener("message", (event) => {
  const data = event.data;
  if (data.type) {
    listeners.forEach((fn) => fn(data));
    return;
  }
  const item = pending.get(data.requestId);
  if (!item) return;
  pending.delete(data.requestId);
  if (data.error)
    item.reject(
      Object.assign(new Error(data.error.message), {
        code: data.error.code,
        details: data.error.details,
      }),
    );
  else item.resolve(data.result);
});
export function subscribe(fn: (e: NativeEvent) => void) {
  listeners.add(fn);
  return () => {
    listeners.delete(fn);
  };
}
export function operation<T>(method: string, payload: unknown = {}, timeoutMs?: number) {
  const id = crypto.randomUUID();
  let timer: number | undefined;
  const promise = new Promise<T>((resolve, reject) => {
    if (!native) {
      reject(
        new Error(
          "Tính năng này cần mở bằng LSCode.WebView.exe. Trình duyệt chỉ xem được giao diện.",
        ),
      );
      return;
    }
    pending.set(id, { resolve: resolve as (v: unknown) => void, reject });
    if (timeoutMs !== undefined) {
      timer = window.setTimeout(() => {
        if (!pending.delete(id)) return;
        reject(new Error("Thao tác quá thời gian chờ. Vui lòng thử lại."));
        void request("operation.cancel", { requestId: id }).catch(() => {});
      }, timeoutMs);
    }
    window.chrome!.webview!.postMessage({
      protocolVersion: 1,
      requestId: id,
      method,
      payload,
    });
  });
  return {
    id,
    promise: timeoutMs === undefined ? promise : promise.finally(() => window.clearTimeout(timer)),
    cancel: () => request("operation.cancel", { requestId: id }),
  };
}
export function request<T>(method: string, payload: unknown = {}): Promise<T> {
  return operation<T>(method, payload).promise;
}
export function notifyWindowReady() {
  let frame = requestAnimationFrame(() => {
    frame = requestAnimationFrame(() => void request("window.ready").catch(() => {}));
  });
  return () => cancelAnimationFrame(frame);
}
export function readClipboard(): Promise<string> {
  return native
    ? request<string>("clipboard.read")
    : navigator.clipboard.readText();
}
export function quoteSqlName(schema: string, name: string) {
  const quote = (value: string) => "[" + value.replaceAll("]", "]]") + "]";
  return quote(schema) + "." + quote(name);
}
export function writeClipboard(text: string): Promise<unknown> {
  return native
    ? request("clipboard.write", { text })
    : navigator.clipboard.writeText(text);
}
