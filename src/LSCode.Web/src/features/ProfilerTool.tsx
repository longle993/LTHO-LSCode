import { useEffect, useMemo, useRef, useState } from "react";
import { Play, Square } from "lucide-react";
import { Button, DataGrid, WorkbenchNotice } from "../ui";
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
    [layout, setLayout] = useState(false),
    [notice, setNotice] = useState("");
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
  async function copySql(text: string) {
    await writeClipboard(text.replaceAll("\0", ""));
    setNotice("Đã sao chép SQL.");
  }
  useEffect(() => {
    if (!notice) return;
    const timer = window.setTimeout(() => setNotice(""), 2500);
    return () => window.clearTimeout(timer);
  }, [notice]);
  const displayRows = useMemo(() => arrangeProfilerEvents(rows), [rows]);
  const table = useMemo(() => ({
    columns: ["Thời gian", "Event", "Database", "Login", "ID", "Duration ms", "Reads", "SQL"],
    rows: displayRows.map(({ event: r }) => [
      r.time,
      r.event,
      r.database,
      r.login,
      r.application,
      String(r.durationMs),
      String(r.reads),
      r.text,
    ]),
  }), [displayRows]);
  const sqlColumnIndex = table.columns.length - 1;
  const sqlText = (followLatest ? displayRows.at(-1)?.event.text : selected) ?? "";
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
            if (followLatest) setSelected(displayRows.at(-1)?.event.text ?? "");
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
            if (followLatest) setSelected(displayRows.at(-1)?.event.text ?? "");
            setFollowLatest(false);
          }}
          onRow={(r) => setSelected(r[sqlColumnIndex] ?? "")}
          onCellDoubleClick={(_, column, values) => {
            const sql = values[sqlColumnIndex] ?? "";
            if (column !== sqlColumnIndex || !sql) return;
            setSelected(sql);
            setFollowLatest(false);
            void task.run(() => copySql(sql));
          }}
          rowClassName={(_, index) => displayRows[index]?.errorKind === "source"
            ? "profiler-error-source-row"
            : displayRows[index]?.errorKind === "message" ? "profiler-error-message-row" : undefined}
        />
        <div className="trace-detail" style={layout ? { flex: 1, maxHeight: "none" } : undefined}>
          <div className="trace-detail-toolbar">
            <span>SQL đang chọn</span>
            <Button
              variant="ghost"
              disabled={!sqlText || task.busy}
              onClick={() => void task.run(() => copySql(sqlText))}
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
              const modifier = event.ctrlKey || event.metaKey;
              const key = event.key.toLowerCase();
              if (modifier && key === "a") {
                event.preventDefault();
                event.stopPropagation();
                event.currentTarget.setSelectionRange(0, event.currentTarget.value.length);
              } else if (modifier && key === "c") {
                event.preventDefault();
                event.stopPropagation();
                const { selectionStart, selectionEnd } = event.currentTarget;
                const visibleText = event.currentTarget.value;
                const text = selectionStart === selectionEnd ||
                  (selectionStart === 0 && selectionEnd === visibleText.length)
                  ? sqlText
                  : visibleText.slice(selectionStart, selectionEnd);
                void task.run(() => copySql(text));
              }
            }}
          />
        </div>
      </div>
      <WorkbenchNotice status={notice} onClose={() => setNotice("")} />
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
  severity?: number;
  text: string;
};

type DisplayTraceEvent = { event: TraceEvent; errorKind?: "source" | "message" };

function eventKind(event: TraceEvent) {
  const name = event.event.toLowerCase().replaceAll(/[^a-z0-9]+/g, "");
  const message = event.text.trimStart().toLowerCase();
  const warning = message.startsWith("warning:") || message.startsWith("cảnh báo:") ||
    (typeof event.severity === "number" && event.severity > 0 && event.severity < 11);
  return {
    error: (name.includes("error") || name.includes("exception")) && !warning,
    starting: name.includes("starting"),
    completed: name.includes("completed"),
    family: name.includes("rpc") ? "rpc" : name.includes("batch") ? "batch" : "",
  };
}

function correlationKey(event: TraceEvent) {
  return event.session || `${event.database}\u0000${event.login}`;
}

function timeDistance(left: TraceEvent, right: TraceEvent, leftIndex: number, rightIndex: number) {
  const leftTime = Date.parse(left.time), rightTime = Date.parse(right.time);
  return Number.isFinite(leftTime) && Number.isFinite(rightTime)
    ? Math.abs(leftTime - rightTime)
    : Math.abs(leftIndex - rightIndex) * 100;
}

function arrangeProfilerEvents(events: TraceEvent[]): DisplayTraceEvent[] {
  const starts = events
    .map((event, index) => ({ event, index, kind: eventKind(event) }))
    .filter((item) => item.kind.starting);
  const usedStarts = new Set<number>();
  const errorPairs = new Map<number, number>();

  events.forEach((error, errorIndex) => {
    if (!eventKind(error).error) return;
    const candidates = starts.filter(({ event, index }) =>
      !usedStarts.has(index) &&
      correlationKey(event) === correlationKey(error) &&
      Math.abs(index - errorIndex) <= 12 &&
      timeDistance(event, error, index, errorIndex) <= 30_000,
    );
    const match = candidates.sort((left, right) =>
      timeDistance(left.event, error, left.index, errorIndex) -
        timeDistance(right.event, error, right.index, errorIndex) ||
      Math.abs(left.index - errorIndex) - Math.abs(right.index - errorIndex),
    )[0];
    if (!match) return;
    usedStarts.add(match.index);
    errorPairs.set(errorIndex, match.index);
  });

  const anchors = new Map<number, { start: number; error: number }>();
  const pairedIndexes = new Set<number>();
  errorPairs.forEach((start, error) => {
    anchors.set(Math.min(start, error), { start, error });
    pairedIndexes.add(start);
    pairedIndexes.add(error);
  });

  const failedCompletions = new Set<number>();
  errorPairs.forEach((startIndex, errorIndex) => {
    const start = events[startIndex], startKind = eventKind(start);
    const after = Math.max(startIndex, errorIndex);
    const completion = events.findIndex((candidate, index) => {
      const kind = eventKind(candidate);
      return index > after && index - after <= 12 && kind.completed &&
        correlationKey(candidate) === correlationKey(start) &&
        (!startKind.family || kind.family === startKind.family) &&
        (!start.text || !candidate.text || candidate.text === start.text);
    });
    if (completion >= 0) failedCompletions.add(completion);
  });

  const result: DisplayTraceEvent[] = [];
  events.forEach((event, index) => {
    const pair = anchors.get(index);
    if (pair) {
      result.push({ event: events[pair.start], errorKind: "source" });
      result.push({ event: events[pair.error], errorKind: "message" });
      return;
    }
    if (pairedIndexes.has(index) || failedCompletions.has(index)) return;
    const kind = eventKind(event);
    if (kind.starting) return;
    result.push({ event, errorKind: kind.error ? "message" : undefined });
  });
  return result;
}
