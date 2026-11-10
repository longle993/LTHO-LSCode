import { lazy, Suspense, useEffect, useState } from "react";
import { request, native, subscribe } from "./bridge";
import type { Bootstrap } from "./bridge";
import { ProjectDialog } from "./features/ProjectDialog";
const load = () => import("./App");
const App = lazy(load);
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
    void request<Bootstrap>("studio.bootstrap")
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
    return subscribe((e) => {
      if (e.type === "window.closing" && !document.querySelector(".studio"))
        void request("window.close");
    });
  }, [entered]);
  if (entered)
    return (
      <Suspense
        fallback={<div className="empty">Đang mở trình soạn thảo…</div>}
      >
        <App bootstrap={bootstrap} initialProjectId={projectId} />
      </Suspense>
    );
  return (
    <div className="startup">
      <div className="brand">
        <span className="brand-mark">LS</span>
        <strong>Studio Code</strong>
      </div>
      {bootstrap ? (
        <ProjectDialog
          projects={bootstrap.projects}
          onProjects={(projects) => setBootstrap({ ...bootstrap, projects })}
          onClose={() => setEntered(true)}
          onOpen={async (p) => {
            setProjectId(p.id);
            setEntered(true);
          }}
        />
      ) : (
        <div className="empty" role="status">
          {error || "Đang tải danh sách dự án…"}
        </div>
      )}
    </div>
  );
}
