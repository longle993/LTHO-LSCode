import { lazy, Suspense, useEffect, useState } from "react";
import logo from "../../../assets/logo2.png";
import { request, native, subscribe, notifyWindowReady } from "./bridge";
import type { Bootstrap } from "./bridge";
import { ProjectDialog } from "./features/ProjectDialog";
const load = () => import("./App");
const App = lazy(load);

function StartupSplash({ label }: { label: string }) {
  return (
    <div className="startup-splash before:animate-studio-layer after:animate-studio-layer motion-reduce:before:animate-none motion-reduce:after:animate-none" role="status">
      <div className="startup-lockup animate-studio-enter motion-reduce:animate-none">
        <div className="startup-brand">
          <img className="app-logo" src={logo} alt="" />
          <strong>Studio Code</strong>
        </div>
        <span>{label}</span>
        <div className="startup-track before:animate-studio-sweep motion-reduce:before:animate-none" aria-hidden="true" />
      </div>
    </div>
  );
}
export default function Startup() {
  const [bootstrap, setBootstrap] = useState<Bootstrap>(),
    [entered, setEntered] = useState(false),
    [projectId, setProjectId] = useState<string>(),
    [error, setError] = useState("");
  const smoke = !!(window as any).__LSCODE_SMOKE__;
  useEffect(() => {
    performance.clearMarks("startup.begin");
    performance.clearMarks("startup.picker");
    performance.clearMeasures("startup.picker");
    performance.mark("startup.begin");
    if (!native || smoke) {
      setEntered(true);
      return;
    }
    void request<Bootstrap>("studio.bootstrap", { deferSnippets: true })
      .then((b) => {
        setBootstrap(b);
        performance.mark("startup.picker");
        performance.measure(
          "startup.picker",
          "startup.begin",
          "startup.picker",
        );
        requestAnimationFrame(() => void load());
      })
      .catch((e) => setError(e.message));
  }, []);
  useEffect(() => {
    if (!native || (!bootstrap && !error)) return;
    return notifyWindowReady();
  }, [bootstrap, error]);
  useEffect(() => {
    return subscribe((e) => {
      if (e.type === "window.closing" && !document.querySelector(".studio"))
        void request("window.close");
    });
  }, [entered]);
  useEffect(() => {
    if (entered || !native) return;
    const openWindow = (event: KeyboardEvent) => {
      if (!event.ctrlKey || !event.shiftKey || event.altKey || event.metaKey || event.code !== "KeyN") return;
      event.preventDefault();
      if (!event.repeat) void request("window.new").catch((failure: Error) => setError(failure.message));
    };
    window.addEventListener("keydown", openWindow, true);
    return () => window.removeEventListener("keydown", openWindow, true);
  }, [entered]);
  if (entered)
    return (
      <Suspense
        fallback={<StartupSplash label={projectId ? "Đang mở dự án…" : "Đang mở trình soạn thảo…"} />}
      >
        <App bootstrap={bootstrap} initialProjectId={projectId} />
      </Suspense>
    );
  if (!bootstrap && !error) return <StartupSplash label="Đang khởi động…" />;
  return (
    <div className="startup">
      <div className="brand">
        <img className="brand-mark app-logo" src={logo} alt="" />
        <strong>Studio Code</strong>
      </div>
      {bootstrap ? (
        <ProjectDialog
          projects={bootstrap.projects}
          lastAccess={bootstrap.settings.projectLastAccess}
          onProjects={(projects) => setBootstrap({ ...bootstrap, projects })}
          onClose={() => setEntered(true)}
          onOpen={async (p) => {
            setProjectId(p.id);
            setEntered(true);
          }}
        />
      ) : (
        <div className="empty" role="alert">
          {error}
        </div>
      )}
    </div>
  );
}
