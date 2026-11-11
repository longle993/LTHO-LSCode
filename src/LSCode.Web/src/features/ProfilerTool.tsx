import { useEffect, useMemo, useRef, useState } from "react";
import { Play, Square } from "lucide-react";
import { Button, DataGrid, Select } from "../ui";
import { request, writeClipboard } from "../bridge";
import type { Target } from "../bridge";
import { useTask } from "./useTask";
export function ProfilerTool({ target, visible, closing, onBusyChange }: {
  target?: Target;
  visible: boolean;
  closing: boolean;
  onBusyChange: (busy: boolean) => void;
}) {
  const [user, setUser] = useState("profile"),
    [password, setPassword] = useState("fsd"),
    [profilerPath, setProfilerPath] = useState("");
  const [engine, setEngine] = useState("ssms18");
  const [running, setRunning] = useState(false),
    [{ rows, offset }, setBuffer] = useState<{ rows: TraceEvent[]; offset: number }>({ rows: [], offset: 0 }),
    [gridVersion, setGridVersion] = useState(0),
    [followLatest, setFollowLatest] = useState(true),
    [filter, setFilter] = useState(""),
    [templatePath, setTemplatePath] = useState(""),
    [selected, setSelected] = useState(""),
    [dropped, setDropped] = useState(0),
    [layout, setLayout] = useState(false);
  const task = useTask();
  useEffect(() => onBusyChange(task.busy), [task.busy, onBusyChange]);
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
    };
  }, []);
  useEffect(() => {
    if (!running || closing) return;
    let current = true;
    let pending = false;
    const timer = setInterval(async () => {
      if (pending) return;
      pending = true;
      try {
        const b = await request<{ events: TraceEvent[]; dropped: number }>(
          "profiler.read",
        );
        if (current) {
          if (b.events.length) setBuffer((old) => {
            const combined = [...old.rows, ...b.events];
            const removed = Math.max(0, combined.length - 5000);
            return { rows: combined.slice(removed), offset: old.offset + removed };
          });
          setDropped(b.dropped);
          setPollError("");
        }
      } catch (e) {
        if (current) setPollError((e as Error).message);
      } finally {
        pending = false;
      }
    }, 700);
    return () => { current = false; clearInterval(timer); };
  }, [running, closing]);
  function replaceRows(events: TraceEvent[]) {
    setBuffer({ rows: events, offset: 0 });
    setGridVersion((old) => old + 1);
    setSelected("");
  }
  const table = useMemo(() => ({
    columns: ["Thời gian", "Event", "Database", "Login", "Duration ms", "Reads", "SQL"],
    rows: rows.map((r) => [r.time, r.event, r.database, r.login, String(r.durationMs), String(r.reads), r.text]),
  }), [rows]);
  const sqlText = (followLatest ? rows.at(-1)?.text : selected) ?? "";
  return (
    <div className="tool-body profiler-body" ref={(element) => { if (element) element.inert = closing; }}>
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
              setFollowLatest(true);
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
        <Button onClick={() => replaceRows([])}>Xóa</Button>
        <Button
          aria-pressed={followLatest}
          variant={followLatest ? "secondary" : "outline"}
          onClick={() => {
            if (followLatest) setSelected(rows.at(-1)?.text ?? "");
            setFollowLatest(!followLatest);
          }}
        >
          Theo dõi cuối lưới
        </Button>
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
                replaceRows(normalized);
                setFollowLatest(true);
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
        className={layout ? "profiler-results tool-columns" : "profiler-results"}
        style={{
          display: "flex",
          flexDirection: layout ? "row" : "column",
          flex: 1,
          gap: 12,
        }}
      >
        <DataGrid
          key={gridVersion}
          table={table}
          rangeSelection
          followEnd={visible && followLatest}
          rowOffset={offset}
          onInspect={() => {
            if (followLatest) setSelected(rows.at(-1)?.text ?? "");
            setFollowLatest(false);
          }}
          onRow={(r) => setSelected(r[6] ?? "")}
        />
        <div className="trace-detail" style={layout ? { flex: 1, maxHeight: "none" } : undefined}>
          <div className="trace-detail-toolbar">
            <span>SQL đang chọn</span>
            <Button
              variant="ghost"
              disabled={!sqlText || task.busy}
              onClick={() => void task.run(() => writeClipboard(sqlText))}
            >Sao chép SQL</Button>
          </div>
          <textarea
            className="trace-sql"
            aria-label="Nội dung SQL Profiler"
            readOnly
            spellCheck={false}
            placeholder="Chọn một dòng để xem SQL."
            value={sqlText}
            onFocus={() => {
              if (followLatest) {
                setSelected(sqlText);
                setFollowLatest(false);
              }
            }}
            onKeyDown={(event) => {
              if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "c") {
                event.preventDefault();
                event.stopPropagation();
                const { selectionStart, selectionEnd } = event.currentTarget;
                const text = selectionStart === selectionEnd ? sqlText : sqlText.slice(selectionStart, selectionEnd);
                void task.run(() => writeClipboard(text));
              }
            }}
          />
        </div>
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
