import { useEffect, useId, useMemo, useRef, useState } from "react";
import { Play, RefreshCw } from "lucide-react";
import { Button, DataGrid, IconButton, Modal } from "../ui";
import { operation, quoteSqlName, request, writeClipboard } from "../bridge";
import type { Table, Target } from "../bridge";
import { useTask } from "./useTask";

type TableName = { schema: string; table: string };
const tableCatalogCache = new Map<string, { tables: TableName[]; expires: number }>();
const tableNames = (result: Table) => result.rows.flatMap(([schema, table]) =>
  schema && table ? [{ schema, table }] : []);
type Preview = { data: Table; snapshotId: string };
type ColumnMenu = { x: number; y: number; columns: string[] };
type Suggestion = { label: string; value: string; caret?: number };
type Clause = "select" | "where" | "order";
const ident = /^(?:\[((?:[^\]]|\]\])+)\]|([^.[\]]+))(?:\s*\.\s*(?:\[((?:[^\]]|\]\])+)\]|([^.[\]]+)))?$/u;
const parseTable = (input: string): TableName | undefined => {
  const match = ident.exec(input.trim());
  if (!match) return;
  const first = match[1]?.replaceAll("]]", "]") ?? match[2]?.trim();
  const second = match[3]?.replaceAll("]]", "]") ?? match[4]?.trim();
  if (!first || (second !== undefined && !second)) return;
  return second ? { schema: first, table: second } : { schema: "dbo", table: first };
};
const quote = (name: string) => "[" + name.replaceAll("]", "]]") + "]";
const functions = new Set(["ISNULL", "COALESCE", "COUNT", "SUM", "AVG", "MIN", "MAX", "GETDATE", "DATEADD", "DATEDIFF", "CAST", "CONVERT", "ROW_NUMBER"]);
const clauseKeywords: Record<Clause, string[]> = {
  select: ["AS", "CASE", "WHEN", "THEN", "ELSE", "END", "NULL", "OVER", "PARTITION BY"],
  where: ["AND", "OR", "NOT", "IN", "NOT IN", "LIKE", "BETWEEN", "IS NULL", "IS NOT NULL", "NULL", "EXISTS", "NOT EXISTS", "CASE", "WHEN", "THEN", "ELSE", "END"],
  order: ["ASC", "DESC", "CASE", "WHEN", "THEN", "ELSE", "END"],
};
function clauseSuggestions(input: string, caret: number, columns: string[], syntax: string[], clause: Clause): Suggestion[] {
  const before = input.slice(0, caret);
  if ((before.match(/'/g)?.length ?? 0) % 2 !== 0) return [];
  const tail = before.match(/(?:\[[^\]]*|[\p{L}\p{N}_$#@]*)$/u)?.[0] ?? "";
  const prefix = before.slice(0, before.length - tail.length);
  const search = tail.replace(/^\[/, "").toLowerCase();
  const after = input.slice(caret);
  const suffix = (tail.startsWith("[") ? after.match(/^(?:[^\]]|\]\])*\]/)?.[0] : undefined)
    ?? after.match(/^[\p{L}\p{N}_$#@]*/u)?.[0] ?? "";
  const rest = input.slice(caret + suffix.length);
  const phrase = before.match(/\b(?:IS(?:\s+NOT)?|NOT|PARTITION)\s+[a-z]*$/i)?.[0];
  const candidates = [
    ...columns.map((name) => ({ match: name, label: quote(name), text: quote(name), inside: false })),
    ...syntax.filter((keyword) => functions.has(keyword) || clauseKeywords[clause].includes(keyword))
      .map((keyword) => ({ match: keyword, label: keyword, text: functions.has(keyword) ? keyword + "()" : keyword, inside: functions.has(keyword) })),
  ];
  return candidates.filter(({ match }) => match.toLowerCase().includes(search))
    .sort((a, b) => Number(b.match.toLowerCase().startsWith(search)) - Number(a.match.toLowerCase().startsWith(search)))
    .slice(0, 40)
    .map(({ label, text, inside }) => {
      const head = phrase && label.startsWith(phrase.toUpperCase()) ? before.slice(0, -phrase.length) : prefix;
      return { label, value: head + text + rest, caret: head.length + text.length - Number(inside) };
    });
}

function SuggestedInput({ label, placeholder, value, options, disabled, onChange, onChoose, onSubmit, onCaret, className }: {
  label: string; placeholder?: string; value: string; options: Suggestion[]; disabled: boolean;
  onChange: (value: string) => void; onChoose?: (value: string) => void;
  onSubmit?: () => void; onCaret?: (position: number) => void; className?: string;
}) {
  const id = useId();
  const inputElement = useRef<HTMLInputElement>(null);
  const [open, setOpen] = useState(false), [active, setActive] = useState(-1);
  const visible = open && options.length > 0 && !disabled;
  const current = active < options.length ? active : -1;
  useEffect(() => {
    if (visible && current >= 0) document.getElementById(id + "-" + current)?.scrollIntoView({ block: "nearest" });
  }, [visible, current, id]);
  function choose(option: Suggestion) {
    onChange(option.value);
    onChoose?.(option.value);
    setOpen(false);
    setActive(-1);
    if (option.caret !== undefined) requestAnimationFrame(() => {
      inputElement.current?.setSelectionRange(option.caret!, option.caret!);
      onCaret?.(option.caret!);
    });
  }
  return (
    <div className={`relative ${className ?? ""}`}>
      <input ref={inputElement} role="combobox" aria-label={label} aria-autocomplete="list"
        aria-expanded={visible} aria-controls={visible ? id : undefined}
        aria-activedescendant={visible && current >= 0 ? id + "-" + current : undefined}
        placeholder={placeholder ?? label} value={value} disabled={disabled}
        onFocus={() => setOpen(true)} onBlur={() => setOpen(false)}
        onSelect={(event) => onCaret?.(event.currentTarget.selectionStart ?? 0)}
        onChange={(event) => {
          setOpen(true);
          setActive(-1);
          onCaret?.(event.currentTarget.selectionStart ?? event.target.value.length);
          onChange(event.target.value);
        }}
        onKeyDown={(event) => {
          if (event.ctrlKey && event.code === "Space") { event.preventDefault(); setOpen(true); }
          if (event.key === "Escape") { setOpen(false); setActive(-1); }
          if ((event.key === "ArrowDown" || event.key === "ArrowUp") && options.length) {
            event.preventDefault();
            setOpen(true);
            setActive((event.key === "ArrowDown" ? current + 1 : current <= 0 ? options.length - 1 : current - 1) % options.length);
          }
          if (event.key === "Enter" && visible && (current >= 0 || onChoose)) {
            event.preventDefault();
            choose(options[Math.max(0, current)]);
          } else if (event.key === "Enter") onSubmit?.();
        }} />
      {visible && <ul id={id} role="listbox" className="table-suggestions">
        {options.map((option, index) => (
          <li key={option.label} id={id + "-" + index} role="option" aria-selected={index === current}
            onPointerDown={(event) => event.preventDefault()} onClick={() => choose(option)}>
            {option.label}
          </li>
        ))}
      </ul>}
    </div>
  );
}

export function TableTool({ scope, target, onText, onConnect, onTableChange }: {
  scope: string;
  target?: Target;
  onText: (text: string, name: string) => void;
  onConnect: () => void;
  onTableChange: (table: string) => void;
}) {
  const [input, setInput] = useState(""), [where, setWhere] = useState(""),
    [orderBy, setOrderBy] = useState(""), [columns, setColumns] = useState(""),
    [mode, setMode] = useState<"data" | "structure">("data"),
    [catalog, setCatalog] = useState<{ targetId: string; tables: TableName[] }>(),
    [catalogRevision, setCatalogRevision] = useState(0),
    [quickCatalog, setQuickCatalog] = useState<{ targetId: string; query: string; tables: TableName[] }>(),
    [catalogError, setCatalogError] = useState(""),
    [syntax, setSyntax] = useState<string[]>([]),
    [carets, setCarets] = useState<Record<Clause, number>>({ select: 0, where: 0, order: 0 }),
    [structure, setStructure] = useState<{ key: string; data: Table }>(),
    [preview, setPreview] = useState<{ key: string; result: Preview }>(),
    [selectedRows, setSelectedRows] = useState<number[]>([]),
    [menu, setMenu] = useState<ColumnMenu>(),
    [drop, setDrop] = useState<string[]>(),
    [scriptMenu, setScriptMenu] = useState(false),
    [status, setStatus] = useState("");
  const task = useTask();
  const reading = useRef<ReturnType<typeof operation<Preview>>>();
  const contextMenu = useRef<HTMLDivElement>(null);
  const scriptControls = useRef<HTMLDivElement>(null);
  const name = parseTable(input);
  const targetId = target?.id;
  const schema = name?.schema, table = name?.table;
  const key = JSON.stringify([targetId, schema, table]);
  const tables = catalog?.targetId === targetId ? catalog?.tables : undefined;
  const query = input.replace(/[\[\]]/g, "").trim().toLowerCase();
  const candidates = tables ?? (quickCatalog?.targetId === targetId && quickCatalog?.query === query ? quickCatalog.tables : undefined);
  const knownTable = !!candidates?.some((entry) =>
    entry.schema.toLowerCase() === schema?.toLowerCase() && entry.table.toLowerCase() === table?.toLowerCase());
  const names = useMemo(() => {
    const terms = query.split(/\s+/).filter(Boolean);
    const ranked: Suggestion[][] = [[], [], []];
    for (const { schema, table } of candidates ?? []) {
      const short = table.toLowerCase();
      const qualified = schema.toLowerCase() + "." + short;
      if (!terms.every((term) => qualified.includes(term))) continue;
      const rank = short === query || qualified === query ? 0 : short.startsWith(query) || qualified.startsWith(query) ? 1 : 2;
      if (ranked[rank].length >= 40) continue;
      const value = schema === "dbo" && /^[\p{L}\p{N}_$#@]+$/u.test(table) ? table : quoteSqlName(schema, table);
      ranked[rank].push({ label: value, value });
      if (!query && ranked[rank].length === 40) break;
    }
    return ranked.flat().slice(0, 40);
  }, [candidates, query]);
  const currentStructure = structure?.key === key ? structure.data : undefined;
  const currentPreview = preview?.key === key ? preview.result : undefined;
  const fieldNames = currentStructure?.rows.flatMap((row) => row[0] ? [row[0]] : []) ?? [];
  const payload = { targetId, schema, table };
  const canRead = !!targetId && !!name && !task.busy;
  useEffect(() => {
    setCatalog(undefined);
    setCatalogError("");
    if (!targetId) return;
    const cached = tableCatalogCache.get(targetId);
    if (cached && cached.expires > Date.now()) {
      setCatalog({ targetId, tables: cached.tables });
      return;
    }
    let current = true;
    const pending = operation<Table>("table.catalog", { targetId });
    void pending.promise.then((result) => {
      if (!current) return;
      const tables = tableNames(result);
      if (tableCatalogCache.size >= 16) {
        const oldest = tableCatalogCache.keys().next().value;
        if (oldest) tableCatalogCache.delete(oldest);
      }
      tableCatalogCache.set(targetId, { tables, expires: Date.now() + 300_000 });
      setCatalog({ targetId, tables });
    }).catch((error) => { if (current) setCatalogError((error as Error).message); });
    return () => {
      current = false;
      void pending.cancel().catch(() => {});
    };
  }, [targetId, catalogRevision]);
  useEffect(() => {
    if (!targetId || tables) return;
    const cached = tableCatalogCache.get(targetId);
    if (cached && cached.expires > Date.now()) return;
    let current = true;
    let pending: ReturnType<typeof operation<Table>> | undefined;
    const timer = window.setTimeout(() => {
      pending = operation<Table>("table.names", { targetId, query });
      void pending.promise.then((result) => {
        if (current) setQuickCatalog({ targetId, query, tables: tableNames(result) });
      }).catch(() => {});
    }, query ? 90 : 0);
    return () => {
      current = false;
      window.clearTimeout(timer);
      void pending?.cancel().catch(() => {});
    };
  }, [targetId, tables, query, catalogRevision]);
  useEffect(() => {
    let current = true;
    void request<{ text: string; isSnippet: boolean }[]>("sql.complete", { text: "", beforeCaret: "" })
      .then((items) => {
        if (current) setSyntax(items.filter((item) => !item.isSnippet).map((item) => item.text));
      }).catch(() => {});
    return () => { current = false; };
  }, []);
  useEffect(() => {
    setSelectedRows([]);
    setMenu(undefined);
    setDrop(undefined);
    setStatus("");
    if (!targetId || !schema || !table || !knownTable) return;
    let current = true;
    let pending: ReturnType<typeof operation<Table>> | undefined;
    const timer = window.setTimeout(() => {
      pending = operation<Table>("table.structure", { targetId, schema, table });
      void pending.promise.then((data) => {
        if (current) setStructure({ key, data });
      }).catch(() => {});
    }, 80);
    return () => {
      current = false;
      window.clearTimeout(timer);
      void pending?.cancel().catch(() => {});
    };
  }, [key, targetId, schema, table, knownTable]);
  useEffect(() => () => {
    const pending = reading.current;
    if (pending) void pending.cancel().catch(() => {});
    void (pending?.promise.catch(() => {}) ?? Promise.resolve())
      .then(() => request("table.releaseSnapshot", { scope })).catch(() => {});
  }, [scope]);
  useEffect(() => {
    if (!menu && !scriptMenu) return;
    function dismiss(event: PointerEvent) {
      const element = event.target as Node;
      if (!contextMenu.current?.contains(element)) setMenu(undefined);
      if (!scriptControls.current?.contains(element)) setScriptMenu(false);
    }
    function escape(event: KeyboardEvent) {
      if (event.key === "Escape") { setMenu(undefined); setScriptMenu(false); }
    }
    document.addEventListener("pointerdown", dismiss);
    document.addEventListener("keydown", escape);
    if (menu) contextMenu.current?.querySelector("button")?.focus();
    return () => {
      document.removeEventListener("pointerdown", dismiss);
      document.removeEventListener("keydown", escape);
    };
  }, [menu, scriptMenu]);

  const readStructure = async (chosen = name) => {
    if (!chosen || !targetId) return;
    onTableChange(chosen.schema === "dbo" ? chosen.table : chosen.schema + "." + chosen.table);
    const data = await request<Table>("table.structure", { targetId, ...chosen });
    setStructure({ key: JSON.stringify([targetId, chosen.schema, chosen.table]), data });
    setSelectedRows([]);
  };
  const readData = (chosen = name, filters = { where, orderBy, select: columns }) => task.run(async () => {
    if (!chosen || !targetId) return;
    onTableChange(chosen.schema === "dbo" ? chosen.table : chosen.schema + "." + chosen.table);
    const op = operation<Preview>("table.view", { scope, targetId, ...chosen, ...filters });
    reading.current = op;
    try { setPreview({ key: JSON.stringify([targetId, chosen.schema, chosen.table]), result: await op.promise }); }
    finally { if (reading.current === op) reading.current = undefined; }
  });
  function chooseTable(value: string) {
    const chosen = parseTable(value);
    if (!chosen) return;
    const changing = chosen.schema !== schema || chosen.table !== table;
    if (changing) {
      setWhere("");
      setOrderBy("");
      setColumns("");
      setCarets({ select: 0, where: 0, order: 0 });
    }
    if (mode === "data") void readData(chosen, changing ? { where: "", orderBy: "", select: "" } : undefined);
    else void task.run(() => readStructure(chosen));
  }
  function generateScript(kind: "create" | "insert") {
    setScriptMenu(false);
    void task.run(async () => {
      if (!table || !schema || !targetId) return;
      if (kind === "insert") {
        if (currentPreview?.data.rows.length) {
          const script = await request<string>("table.insertScript", { scope, snapshotId: currentPreview.snapshotId });
          onText(script, table + "-insert.sql");
        }
        return;
      }
      const script = await request<string>("table.createScript", payload);
      onText(script, table + "-create.sql");
    });
  }
  const view = mode === "structure" ? currentStructure : currentPreview?.data;
  return (
    <div className="table-inspection flex min-h-0 min-w-0 flex-1 flex-col gap-2 overflow-hidden p-3">
      <div className="flex shrink-0 flex-wrap items-center gap-2">
        <SuggestedInput label="Bảng" placeholder="Tên bảng hoặc schema.bảng" options={names} value={input}
          onChange={setInput} onChoose={chooseTable} disabled={task.busy} className="table-name-input"
          onSubmit={() => { if (canRead) void (mode === "data" ? readData() : task.run(readStructure)); }} />
        {targetId && <IconButton label="Tải lại danh sách bảng" onClick={() => {
          tableCatalogCache.delete(targetId);
          setQuickCatalog(undefined);
          setCatalogRevision((version) => version + 1);
        }}><RefreshCw size={15} /></IconButton>}
        <Button aria-pressed={mode === "data"} variant={mode === "data" ? "secondary" : "ghost"} onClick={() => setMode("data")}>Dữ liệu</Button>
        <Button aria-pressed={mode === "structure"} variant={mode === "structure" ? "secondary" : "ghost"} onClick={() => setMode("structure")}>Cấu trúc</Button>
        <div ref={scriptControls} className="relative">
          <Button aria-expanded={scriptMenu} disabled={!canRead} onClick={() => setScriptMenu(!scriptMenu)}>Script</Button>
          {scriptMenu && (
            <div className="table-action-menu absolute top-full left-0 z-10" aria-label="Loại script">
              <Button variant="ghost" onClick={() => generateScript("create")}>Script tạo bảng</Button>
              <Button variant="ghost" disabled={mode !== "data" || !currentPreview?.data.rows.length} onClick={() => generateScript("insert")}>Script INSERT dữ liệu đang hiển thị</Button>
            </div>
          )}
        </div>
        {target ? <span className="muted truncate" title={target.server + " / " + target.database}>{target.database} · {view?.rows.length ?? 0} dòng</span>
          : <Button onClick={onConnect}>Kết nối SQL</Button>}
      </div>
      {targetId && (!tables || !names.length) && <p className={catalogError ? "form-error" : "muted"} role={catalogError ? "alert" : "status"}>
        {catalogError || (tables ? "Không tìm thấy bảng phù hợp." : "Đang tải danh sách bảng…")}
      </p>}
      <div className="flex shrink-0 flex-wrap items-center gap-2">
        {mode === "data" && ([
          ["select", "Cột SELECT", columns, setColumns],
          ["where", "WHERE", where, setWhere],
          ["order", "ORDER BY", orderBy, setOrderBy],
        ] as const).map(([field, label, value, setValue]) => (
          <SuggestedInput key={field} label={label} value={value}
            options={clauseSuggestions(value, Math.min(carets[field], value.length), fieldNames, syntax, field)}
            onCaret={(position) => setCarets((old) => old[field] === position ? old : { ...old, [field]: position })}
            onChange={setValue} disabled={task.busy} className="table-filter flex-1" />
        ))}
        <Button variant="primary" disabled={!canRead} onClick={() => void (mode === "data" ? readData() : task.run(readStructure))}>
          <Play size={14} />{mode === "data" ? "Đọc dữ liệu" : "Đọc cấu trúc"}
        </Button>
        {task.busy && reading.current && <Button onClick={() => void reading.current?.cancel()}>Dừng</Button>}
        {mode === "structure" && fieldNames.length > 0 && <span className="muted">Chọn cột rồi nhấn chuột phải.</span>}
      </div>
      {task.error && <p className="form-error" role="alert">{task.error}</p>}
      {status && <span className="muted" role="status">{status}</span>}
      {view ? (
        <DataGrid key={mode + key} table={view} rangeSelection
          selectedRows={mode === "structure" ? selectedRows : undefined}
          onSelectionChange={mode === "structure" ? setSelectedRows : undefined}
          onRowContextMenu={mode === "structure" && !task.busy ? (row, event) => {
            event.preventDefault();
            const rows = selectedRows.includes(row) ? selectedRows : [row];
            setSelectedRows(rows);
            setMenu({
              x: Math.max(8, Math.min(event.clientX, innerWidth - 224)),
              y: Math.max(8, Math.min(event.clientY, innerHeight - 132)),
              columns: rows.flatMap((index) => view.rows[index]?.[0] ? [view.rows[index][0]!] : []),
            });
          } : undefined} />
      ) : <div className="empty">{task.busy ? "Đang đọc bảng…" : "Chọn bảng để xem cấu trúc hoặc đọc tối đa 1.000 dòng dữ liệu."}</div>}
      {menu && (
        <div ref={contextMenu} className="table-action-menu fixed z-20" style={{ left: menu.x, top: menu.y }} aria-label="Thao tác cột">
          {([["addScript", "ADD COLUMN"], ["alterScript", "ALTER COLUMN"]] as const).map(([method, label]) =>
            <Button key={method} variant="ghost" onClick={() => {
              setMenu(undefined);
              void task.run(async () => {
                const script = await request<string>("table." + method, { ...payload, columns: menu.columns });
                await writeClipboard(script);
                setStatus("Đã sao chép script " + label + ".");
              });
            }}>Gen {label}</Button>)}
          <Button variant="ghost" className="table-drop-action" onClick={() => {
            setMenu(undefined); setDrop(menu.columns);
          }}>DROP COLUMN</Button>
        </div>
      )}
      {drop && (
        <Modal title="Xóa cột khỏi bảng" onClose={() => { if (!task.busy) setDrop(undefined); }}>
          <div className="tool-body">
            <p>Xóa {drop.map(quote).join(", ")} khỏi {name && quoteSqlName(name.schema, name.table)} trên {target?.database}? Dữ liệu của các cột này sẽ bị xóa.</p>
            {task.error && <p className="form-error" role="alert">{task.error}</p>}
            <div className="flex flex-wrap justify-end gap-2">
              <Button disabled={task.busy} onClick={() => setDrop(undefined)}>Hủy</Button>
              <Button disabled={task.busy} className="table-drop-action" onClick={() => void task.run(async () => {
                await request("table.dropColumns", { ...payload, columns: drop });
                setPreview(undefined);
                setDrop(undefined);
                await readStructure();
                setStatus("Đã xóa cột.");
              })}>Xóa cột</Button>
            </div>
          </div>
        </Modal>
      )}
    </div>
  );
}
