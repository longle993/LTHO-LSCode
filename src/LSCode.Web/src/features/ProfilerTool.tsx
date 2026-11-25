import { useEffect, useMemo, useRef, useState } from "react";
import { Play, Square } from "lucide-react";
import { Button, DataGrid } from "../ui";
import { request, writeClipboard } from "../bridge";
import type { Target } from "../bridge";
import { useTask } from "./useTask";
type ProfilerEngine = "ssms18" | "sqlTrace" | "extendedEvents";
export function ProfilerTool({ target, visible, closing, onBusyChange }: {
  target?: Target;
  visible: boolean;
  closing: boolean;
  onBusyChange: (busy: boolean) => void;
}) {
  const [user, setUser] = useState(""),
    [password, setPassword] = useState(""),
    [profilerPath, setProfilerPath] = useState<string>(),
    [defaultPath, setDefaultPath] = useState(""),
    [authentication, setAuthentication] = useState(""),
    [engine, setEngine] = useState<ProfilerEngine>(),
    [captureTarget, setCaptureTarget] = useState<Target>();
  const [running, setRunning] = useState(false),
    [{ rows, offset }, setBuffer] = useState<{ rows: TraceEvent[]; offset: number }>({ rows: [], offset: 0 }),
    [gridVersion, setGridVersion] = useState(0),
    [followLatest, setFollowLatest] = useState(true),
    [filter, setFilter] = useState(""),
    [selected, setSelected] = useState(""),
    [dropped, setDropped] = useState(0),
    [layout, setLayout] = useState(false);
  const task = useTask();
  useEffect(() => onBusyChange(task.busy), [task.busy, onBusyChange]);
  const autoStarted = useRef(false);
  const canStart = useRef(true);
  useEffect(() => {
    canStart.current = !closing;
    return () => { canStart.current = false; };
  }, [closing]);
  const currentTarget = captureTarget ?? target;
  const [pollError, setPollError] = useState("");

  async function startCapture(connection: Target, automatic = false) {
    await task.run(async () => {
      setCaptureTarget(connection);
      setPollError("");
      if (automatic) {
        const defaults = await request<{ profilerPath: string; filter: string; authentication: string }>(
          "profiler.defaults", { targetId: connection.id },
        );
        if (!canStart.current) return;
        setDefaultPath(defaults.profilerPath);
        setFilter(defaults.filter);
        setAuthentication(defaults.authentication);
      }
      const overrideUser = !automatic && user.trim();
      const result = await request<{ engine: ProfilerEngine }>("profiler.start", {
        targetId: connection.id,
        engine: "auto",
        useTargetCredentials: false,
        ...(overrideUser ? { user: overrideUser, password } : {}),
        ...(automatic ? {} : { filter: filter.trim(), profilerPath: profilerPath?.trim() }),
      });
      setEngine(result.engine);
      setDropped(0);
      setFollowLatest(true);
      setRunning(true);
    });
  }
  useEffect(() => {
    if (!currentTarget || !visible || closing || autoStarted.current) return;
    autoStarted.current = true;
    void startCapture(currentTarget, true);
  }, [currentTarget, visible, closing]);
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
      <p className="muted" role="status">
        {currentTarget
          ? `${currentTarget.server} / ${currentTarget.database}${authentication ? ` · ${user.trim() ? "SQL Server" : authentication}` : ""}`
          : "Chọn kết nối SQL trước khi chạy Profiler."}{" "}
        · {running ? "Đang thu thập" : task.busy ? "Đang xử lý" : "Đã dừng"}
        {engine && (engine === "extendedEvents" ? " · Extended Events" : " · SQL Trace")}
        {" "}· {rows.length} sự kiện · Bỏ
        qua {dropped}
      </p>
      <div className="tool-toolbar">
        <input
          aria-label="Người sử dụng Profiler"
          placeholder="Dùng tài khoản kết nối"
          value={user}
          disabled={running || task.busy}
          onChange={(e) => setUser(e.target.value)}
        />
        <input
          aria-label="Mật khẩu Profiler"
          placeholder="Mật khẩu tài khoản tùy chọn"
          type="password"
          value={password}
          disabled={running || task.busy || !user.trim()}
          onChange={(e) => setPassword(e.target.value)}
        />
        <input
          aria-label="Đường dẫn Profiler"
          placeholder="PROFILER.EXE (trống: tự chọn)"
          value={profilerPath ?? defaultPath}
          disabled={running || task.busy}
          onChange={(e) => setProfilerPath(e.target.value)}
        />
        <Button
          disabled={running || task.busy}
          onClick={() =>
            void task.run(async () => {
              const path = await request<string | null>("file.pick");
              if (path) setProfilerPath(path);
            })
          }
        >
          Chọn Profiler.exe
        </Button>
        <input
          aria-label="Bộ lọc database"
          placeholder="Tiền tố database"
          value={filter}
          disabled={running || task.busy}
          onChange={(e) => setFilter(e.target.value)}
        />
        <Button
          variant="primary"
          disabled={running || !currentTarget || task.busy}
          onClick={() => { if (currentTarget) void startCapture(currentTarget); }}
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
