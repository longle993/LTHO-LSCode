import { useEffect, useRef, useState } from "react";
import { Play, Square } from "lucide-react";
import { Button, DataGrid, Select } from "../ui";
import { request } from "../bridge";
import type { ToolProps } from "./types";
import { useTask } from "./useTask";
export function ProfilerTool({ target, onText }: ToolProps) {
  const [user, setUser] = useState("profiler"),
    [password, setPassword] = useState("fsd"),
    [profilerPath, setProfilerPath] = useState("");
  const [engine, setEngine] = useState("ssms18");
  const [running, setRunning] = useState(false),
    [rows, setRows] = useState<TraceEvent[]>([]),
    [filter, setFilter] = useState(""),
    [templatePath, setTemplatePath] = useState(""),
    [selected, setSelected] = useState(""),
    [dropped, setDropped] = useState(0),
    [layout, setLayout] = useState(false);
  const task = useTask();
  const active = useRef(true);
  const [pollError, setPollError] = useState("");
  useEffect(() => {
    active.current = true;
    void request<{ profiler18Path: string }>("settings.read")
      .then((s) => {
        if (active.current) setProfilerPath(s.profiler18Path ?? "");
      })
      .catch(() => {});
    return () => {
      active.current = false;
      void request("profiler.stop").catch(() => {});
    };
  }, []);
  useEffect(() => {
    if (!running) return;
    let pending = false;
    const timer = setInterval(async () => {
      if (pending) return;
      pending = true;
      try {
        const b = await request<{ events: TraceEvent[]; dropped: number }>(
          "profiler.read",
        );
        if (active.current) {
          setRows((old) => [...old, ...b.events].slice(-5000));
          setDropped(b.dropped);
        }
      } catch (e) {
        setPollError((e as Error).message);
        setRunning(false);
      } finally {
        pending = false;
      }
    }, 700);
    return () => clearInterval(timer);
  }, [running]);
  return (
    <div className="tool-body large">
      <p className="muted">
        {target
          ? `${target.server} / ${target.database}`
          : "Chọn kết nối SQL trước khi chạy Profiler."}{" "}
        · {running ? "Đang thu thập" : "Đã dừng"} · {rows.length} sự kiện · Bỏ
        qua {dropped}
      </p>
      <div className="tool-toolbar">
        <input
          aria-label="Profiler user"
          value={user}
          disabled={running}
          onChange={(e) => setUser(e.target.value)}
        />
        <input
          aria-label="Profiler password"
          type="password"
          value={password}
          disabled={running}
          onChange={(e) => setPassword(e.target.value)}
        />
        <input
          aria-label="Đường dẫn Profiler"
          placeholder="Đường dẫn Profiler.exe (trống: dùng Cài đặt)"
          value={profilerPath}
          disabled={running}
          onChange={(e) => setProfilerPath(e.target.value)}
        />
        <Button
          disabled={running}
          onClick={() =>
            void task.run(async () => {
              const path = await request<string | null>("file.pick");
              if (path) setProfilerPath(path);
            })
          }
        >
          Chọn Profiler.exe
        </Button>
        <Select
          label="Profiler engine"
          disabled={running}
          value={engine}
          options={[
            { value: "ssms18", label: "SSMS 18 Trace" },
            { value: "extendedEvents", label: "Extended Events" },
          ]}
          onChange={setEngine}
        />
        <input
          aria-label="Bộ lọc database"
          placeholder="Database prefix (trống: theo target)"
          value={filter}
          disabled={running}
          onChange={(e) => setFilter(e.target.value)}
        />
        <input
          aria-label="Đường dẫn template"
          placeholder="Template .tdf (trống: SQL2014 đi kèm)"
          value={templatePath}
          disabled={running}
          onChange={(e) => setTemplatePath(e.target.value)}
        />
        <Button
          variant="primary"
          disabled={running || !target || task.busy}
          onClick={() =>
            void task.run(async () => {
              await request("profiler.start", {
                targetId: target?.id,
                filter,
                templatePath,
                engine,
                user,
                password,
                profilerPath,
              });
              setRunning(true);
            })
          }
        >
          <Play size={14} />
          Chạy
        </Button>
        <Button
          disabled={!running || task.busy}
          onClick={() =>
            void task.run(async () => {
              await request("profiler.stop");
              setRunning(false);
            })
          }
        >
          <Square size={14} />
          Dừng
        </Button>
        <Button onClick={() => setRows([])}>Xóa</Button>
        <Button onClick={() => setLayout(!layout)}>Đổi bố cục</Button>
        <Button
          onClick={() =>
            void task.run(async () => {
              await request("text.export", {
                name: "trace.json",
                text: JSON.stringify(rows, null, 2),
              });
            })
          }
        >
          Lưu trace
        </Button>
        <Button
          disabled={running}
          onClick={() =>
            void task.run(async () => {
              const opened = await request<
                import("../bridge").DocumentInfo | null
              >("document.open");
              if (!opened) return;
              try {
                const parsed = JSON.parse(opened.text);
                const values = Array.isArray(parsed)
                  ? parsed
                  : (parsed.events ?? parsed.Events);
                if (!Array.isArray(values))
                  throw new Error("Trace JSON không có danh sách sự kiện.");
                const normalized = values
                  .slice(-5000)
                  .map((value) =>
                    Object.fromEntries(
                      Object.entries(value).map(([key, v]) => [
                        key[0].toLowerCase() + key.slice(1),
                        v,
                      ]),
                    ),
                  ) as TraceEvent[];
                setRows(normalized);
              } finally {
                await request("document.close", { id: opened.id });
              }
            })
          }
        >
          Mở trace
        </Button>
      </div>
      <div
        className={layout ? "tool-columns" : ""}
        style={{
          display: "flex",
          flexDirection: layout ? "row" : "column",
          flex: 1,
          minHeight: 0,
          gap: 12,
        }}
      >
        <DataGrid
          table={{
            columns: [
              "Thời gian",
              "Event",
              "Database",
              "Login",
              "Duration ms",
              "Reads",
              "SQL",
            ],
            rows: rows.map((r) => [
              r.time,
              r.event,
              r.database,
              r.login,
              String(r.durationMs),
              String(r.reads),
              r.text,
            ]),
          }}
          onRow={(r) => setSelected(r[6] ?? "")}
        />
        <pre
          className="trace-sql"
          style={layout ? { flex: 1, maxHeight: "none" } : undefined}
        >
          {selected || "Nhấn đúp sự kiện để xem SQL."}
        </pre>
      </div>
      <p className="form-error">{task.error || pollError}</p>
    </div>
  );
}
type TraceEvent = {
  sequence: number;
  time: string;
  event: string;
  database: string;
  login: string;
  host: string;
  application: string;
  session: string;
  durationMs: number;
  reads: number;
  writes: number;
  cpuMs: number;
  text: string;
};
