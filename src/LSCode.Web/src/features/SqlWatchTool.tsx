import { useEffect, useRef, useState } from "react";
import { Bug, Plus, Play, Square, X } from "lucide-react";
import { Button, DataGrid, IconButton, Select } from "../ui";
import { operation, request, type QueryResult, type Target } from "../bridge";

type Watch = { id: number; mode: string; text: string };
export function SqlWatchTool({ targets, initialTarget, onFromEditor, onBusyChange }: {
  targets: Target[];
  initialTarget?: Target;
  onFromEditor: () => string;
  onBusyChange: (busy: boolean) => void;
}) {
  const [targetId, setTargetId] = useState(initialTarget?.id ?? "");
  const [context, setContext] = useState("");
  const [watches, setWatches] = useState<Watch[]>([{ id: 1, mode: "expression", text: "@@SPID" }]);
  const [selected, setSelected] = useState(1);
  const [result, setResult] = useState<QueryResult>();
  const [resultIndex, setResultIndex] = useState(0);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState("");
  const [error, setError] = useState("");
  const [debugAvailable, setDebugAvailable] = useState<boolean>();
  const serial = useRef(1);
  const pending = useRef<ReturnType<typeof operation<QueryResult>>>();
  const working = useRef(false);
  const alive = useRef(true);
  const watch = watches.find(item => item.id === selected)!;
  useEffect(() => { onBusyChange(busy); }, [busy, onBusyChange]);
  useEffect(() => {
    alive.current = true;
    void request<{ available: boolean }>("sql.debug.available").then(value => {
      if (alive.current) setDebugAvailable(value.available);
    }).catch(() => { if (alive.current) setDebugAvailable(false); });
    return () => { alive.current = false; void pending.current?.cancel().catch(() => {}); };
  }, []);
  const clear = () => { setResult(undefined); setError(""); setNotice(""); };
  const change = (value: Partial<Watch>) => { clear(); setWatches(items => items.map(item => item.id === selected ? { ...item, ...value } : item)); };
  async function run() {
    if (!watch?.text.trim() || !targetId || working.current) return;
    working.current = true; setBusy(true); clear();
    const query = watch.mode === "expression" ? `SELECT (${watch.text.trim()}) AS [Watch];` : watch.text;
    // No GO here: DECLARE and table variables in the final context batch stay in scope.
    const sql = context.trim() ? `${context}\n;\n${query}` : query;
    const op = operation<QueryResult>("sql.execute", { targetId, sql }, 180_000);
    pending.current = op;
    try {
      const value = await op.promise;
      if (!alive.current) return;
      setResult(value); setResultIndex(Math.max(0, value.tables.length - 1));
      setNotice(`Watch ${selected} · ${value.durationMs.toFixed(0)} ms${value.truncated ? " · Kết quả đã giới hạn" : ""}`);
    } catch (failure) {
      if (alive.current) setError((failure as { code?: string }).code === "cancelled" ? "Đã dừng Watch." : (failure as Error).message);
    } finally { pending.current = undefined; working.current = false; if (alive.current) setBusy(false); }
  }
  async function debug() {
    if (!targetId || !context.trim() || working.current) return;
    working.current = true; setBusy(true); setError("");
    try {
      const opened = await request<{ path: string }>("sql.debug.open", { targetId, sql: context });
      if (alive.current) setNotice("Đã mở SSDT: " + opened.path + ". Kết nối SQL rồi chọn Execute with Debugger (Alt+F5).");
    } catch (failure) { if (alive.current) setError((failure as Error).message); }
    finally { working.current = false; if (alive.current) setBusy(false); }
  }
  return <div className="flex min-h-0 flex-1 flex-col overflow-auto" aria-label="SQL Debug và Watch">
    <div className="flex flex-wrap items-center gap-2 border-b border-[var(--border)] p-3">
      <Select label="Kết nối Watch" value={targetId} disabled={busy}
        options={[{ value: "", label: "Chọn database" }, ...targets.map(target => ({ value: target.id, label: `${target.database} · ${target.server}` }))]}
        onChange={id => { setTargetId(id); clear(); }} />
      <Button disabled={busy} onClick={() => { setContext(onFromEditor()); clear(); }}>Lấy SQL từ editor</Button>
      <Button disabled={busy || !targetId || !context.trim() || debugAvailable === false} onClick={() => void debug()}><Bug size={15} />Debug bằng SSDT</Button>
    </div>
    <div className="grid shrink-0 gap-3 border-b border-[var(--border)] p-3 lg:grid-cols-2">
      <label className="flex min-w-0 flex-col gap-2">SQL ngữ cảnh / script cần debug
        <textarea aria-label="SQL ngữ cảnh" className="h-32 resize-y font-mono text-xs" spellCheck={false} disabled={busy}
          placeholder="DECLARE @id int = 1;" value={context} onChange={event => { setContext(event.target.value); clear(); }} />
      </label>
      <div className="flex min-w-0 flex-col gap-2">
        <div className="flex flex-wrap items-center gap-2" role="tablist" aria-label="Biểu thức Watch">
          {watches.map(item => <button key={item.id} role="tab" aria-selected={selected === item.id}
            disabled={busy} className={`button ${selected === item.id ? "secondary" : "outline"}`}
            onClick={() => { setSelected(item.id); clear(); }}>Watch {item.id}</button>)}
          <IconButton label="Thêm Watch" disabled={busy || watches.length >= 20} onClick={() => {
            const id = ++serial.current; setWatches(items => [...items, { id, mode: "expression", text: "" }]); setSelected(id); clear();
          }}><Plus size={15} /></IconButton>
          <IconButton label="Xóa Watch đang chọn" disabled={busy || watches.length === 1} onClick={() => {
            const remaining = watches.filter(item => item.id !== selected); setWatches(remaining); setSelected(remaining[0].id); clear();
          }}><X size={15} /></IconButton>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Select label="Loại Watch" disabled={busy} value={watch.mode} onChange={mode => change({ mode })}
            options={[{ value: "expression", label: "Biểu thức / biến" }, { value: "query", label: "Query xem dữ liệu" }]} />
          <Button variant="primary" disabled={busy || !targetId || !watch.text.trim()} onClick={() => void run()}><Play size={15} />Chạy Watch</Button>
          {busy && pending.current && <Button onClick={() => void pending.current?.cancel().catch(failure => setError(failure.message))}><Square size={15} />Dừng Watch</Button>}
        </div>
        <textarea aria-label="Nội dung Watch" className="h-20 resize-y font-mono text-xs" spellCheck={false} disabled={busy}
          value={watch.text} placeholder={watch.mode === "expression" ? "@id" : "SELECT TOP (100) * FROM dbo.TableName"} onChange={event => change({ text: event.target.value })} />
      </div>
    </div>
    <p className="px-3 py-2 text-xs text-[var(--muted)]">Chạy Watch thực thi lại SQL ngữ cảnh và Watch đang chọn trong một kết nối mới. Biến trong stored procedure đang dừng tại breakpoint được xem trong Watch/Locals của SSDT.</p>
    {debugAvailable === false && <p className="px-3 text-xs text-[var(--muted)]">Cần Visual Studio với SQL Server Data Tools để debug từng bước.</p>}
    {(error || notice) && <p role={error ? "alert" : "status"} className={`px-3 py-2 text-xs break-words ${error ? "text-[var(--error-text)]" : "text-[var(--muted)]"}`}>{error || notice}</p>}
    {result && <>
      <div className="flex shrink-0 flex-wrap gap-2 px-3" role="group" aria-label="Kết quả Watch">
        {result.tables.map((table, index) => <Button key={index} aria-pressed={index === resultIndex} onClick={() => setResultIndex(index)}>Kết quả {index + 1} · {table.rows.length} dòng</Button>)}
      </div>
      {result.messages.length > 0 && <details className="shrink-0 px-3 py-2"><summary>Thông báo SQL</summary><pre className="whitespace-pre-wrap text-xs">{result.messages.join("\n")}</pre></details>}
    </>}
    <div className="flex min-h-48 flex-1 flex-col overflow-hidden p-3">
      {result?.tables[resultIndex] ? <DataGrid key={`${selected}:${resultIndex}:${result.durationMs}`} table={result.tables[resultIndex]} rangeSelection />
        : <div className="empty">{busy ? "Đang chạy…" : result ? "Query không trả bảng dữ liệu." : "Chọn kết nối, nhập Watch rồi nhấn Chạy Watch."}</div>}
    </div>
  </div>;
}
