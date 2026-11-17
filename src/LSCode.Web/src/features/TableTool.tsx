import { Component, useEffect, useId, useMemo, useRef, useState, useTransition } from "react";
import type { ChangeEvent, ErrorInfo, KeyboardEvent as ReactKeyboardEvent, ReactNode, SyntheticEvent } from "react";
import { createPortal } from "react-dom";
import { ChevronRight, KeyRound, Play, RefreshCw } from "lucide-react";
import { Button, DataGrid, IconButton, Modal, Select } from "../ui";
import { operation, quoteSqlName, request, writeClipboard } from "../bridge";
import type { Table, Target } from "../bridge";
import { useTask } from "./useTask";
import { TableDataGrid, type TableColumn } from "./TableDataGrid";
import { TableColumnDialog } from "./TableColumnDialog";
import { TableScriptDialog } from "./TableScriptDialog";

type TableName = { schema: string; table: string };
const tableCatalogCache = new Map<string, { tables: TableName[]; expires: number }>();
function isTable(value: unknown): value is Table {
  if (!value || typeof value !== "object") return false;
  const candidate = value as Partial<Table>;
  return Array.isArray(candidate.columns) && candidate.columns.every((column) => typeof column === "string")
    && Array.isArray(candidate.rows) && candidate.rows.every((row) => Array.isArray(row));
}
function normalizeTable(value: unknown, label: string): Table {
  if (!isTable(value)) throw new Error(`Dữ liệu ${label} trả về không đúng định dạng.`);
  return {
    columns: value.columns,
    rows: value.rows.map((row) => row.map((cell) => cell == null ? null : String(cell))),
  };
}
function normalizePreview(value: unknown): Preview {
  if (!value || typeof value !== "object") throw new Error("Kết quả đọc bảng không đúng định dạng.");
  const candidate = value as { data?: unknown; snapshotId?: unknown };
  if (typeof candidate.snapshotId !== "string" || !candidate.snapshotId)
    throw new Error("Kết quả đọc bảng thiếu mã snapshot.");
  return { data: normalizeTable(candidate.data, "đọc bảng"), snapshotId: candidate.snapshotId };
}
function normalizeMetadata(value: unknown): TableColumn[] {
  if (!Array.isArray(value)) throw new Error("Metadata bảng trả về không đúng định dạng.");
  return value.flatMap((entry) => {
    if (!entry || typeof entry !== "object") return [];
    const item = entry as Partial<TableColumn>;
    if (typeof item.name !== "string" || item.name.length === 0 || typeof item.sqlType !== "string") return [];
    return [{
      name: item.name,
      sqlType: item.sqlType,
      key: typeof item.key === "string" ? item.key : "",
      primaryKey: item.primaryKey === true,
      nullable: item.nullable !== false,
      identity: item.identity === true,
      computed: item.computed === true,
      generated: typeof item.generated === "number" ? item.generated : 0,
      writable: item.writable !== false,
    }];
  });
}
const tableNames = (result: unknown) => {
  if (!isTable(result)) return [];
  return result.rows.flatMap(([schema, table]) =>
    schema && table ? [{ schema, table }] : []);
};
type Preview = { data: Table; snapshotId: string };
type ColumnMenu = { x: number; y: number; columns: string[] };
type ScriptPreview = { title: string; script: string };
type ColumnDialog = { mode: "add" | "alter"; column?: string };
type Suggestion = { label: string; value: string; caret?: number };
type Clause = "select" | "where";
const ident = /^(?:\[((?:[^\]]|\]\])+)\]|([^.[\]]+))(?:\s*\.\s*(?:\[((?:[^\]]|\]\])+)\]|([^.[\]]+)))?$/u;
const parseTable = (input: string): TableName | undefined => {
  const match = ident.exec(input.trim());
  if (!match) return;
  const first = match[1]?.replaceAll("]]", "]") ?? match[2]?.trim();
  const second = match[3]?.replaceAll("]]", "]") ?? match[4]?.trim();
  if (!first || (second !== undefined && !second)) return;
  return second ? { schema: first, table: second } : { schema: "dbo", table: first };
};
const tableIdentity = (value?: TableName) => value ? value.schema === "dbo" ? value.table : value.schema + "." + value.table : "";
const quote = (name: string) => "[" + name.replaceAll("]", "]]") + "]";
const functions = new Set(["ISNULL", "COALESCE", "COUNT", "SUM", "AVG", "MIN", "MAX", "GETDATE", "DATEADD", "DATEDIFF", "CAST", "CONVERT", "ROW_NUMBER"]);
const clauseKeywords: Record<Clause, string[]> = {
  select: ["AS", "DISTINCT", "CASE", "WHEN", "THEN", "ELSE", "END", "NULL", "OVER", "PARTITION BY"],
  where: ["AND", "OR", "NOT", "IN", "NOT IN", "LIKE", "BETWEEN", "IS NULL", "IS NOT NULL", "NULL", "EXISTS", "NOT EXISTS", "CASE", "WHEN", "THEN", "ELSE", "END"],
};
function clauseSuggestions(input: string, caret: number, columns: string[], clause: Clause): Suggestion[] {
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
  const phrasePrefix = phrase?.toLowerCase().replace(/\s+/g, " ");
  const candidates = [
    ...columns.map((name) => ({
      match: name, label: name,
      text: tail.startsWith("[") || !/^[\p{L}_][\p{L}\p{N}_$#@]*$/u.test(name) ? quote(name) : name,
      inside: false, phraseMatch: false,
    })),
    ...[...clauseKeywords[clause], ...functions].map((keyword) => ({
      match: keyword, label: keyword, text: functions.has(keyword) ? keyword + "()" : keyword,
      inside: functions.has(keyword), phraseMatch: !!phrasePrefix && keyword.toLowerCase().startsWith(phrasePrefix),
    })),
  ];
  const rank = (candidate: typeof candidates[number]) =>
    candidate.phraseMatch ? 0 : candidate.match.toLowerCase().startsWith(search) ? 1 : 2;
  return candidates.filter(({ match, phraseMatch }) => phraseMatch || match.toLowerCase().includes(search))
    .sort((a, b) => rank(a) - rank(b))
    .slice(0, 40)
    .map(({ label, text, inside, phraseMatch }) => {
      const head = phrase && phraseMatch ? before.slice(0, -phrase.length) : prefix;
      return { label, value: head + text + rest, caret: head.length + text.length - Number(inside) };
    });
}

function SuggestedInput({ label, placeholder, value, options, disabled, focus, onChange, onChoose, onSubmit, onCaret, className, multiline = false }: {
  label: string; placeholder?: string; value: string; options: Suggestion[]; disabled: boolean;
  focus?: boolean;
  onChange: (value: string) => void; onChoose?: (value: string) => void;
  onSubmit?: () => void; onCaret?: (position: number) => void; className?: string; multiline?: boolean;
}) {
  const id = useId();
  const inputElement = useRef<HTMLInputElement | HTMLTextAreaElement | null>(null);
  const [open, setOpen] = useState(false), [active, setActive] = useState(-1);
  const visible = open && options.length > 0 && !disabled;
  const current = active < options.length ? active : -1;
  useEffect(() => {
    if (focus) inputElement.current?.focus();
  }, [focus]);
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
  const handleChange = (event: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    setOpen(true);
    setActive(-1);
    onCaret?.(event.currentTarget.selectionStart ?? event.target.value.length);
    onChange(event.target.value);
  };
  const handleKeyDown = (event: ReactKeyboardEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    if (disabled || event.nativeEvent.isComposing) return;
    if (event.ctrlKey && event.code === "Space") { event.preventDefault(); setOpen(true); }
    if (event.key === "Escape") { setOpen(false); setActive(-1); }
    if ((event.key === "ArrowDown" || event.key === "ArrowUp") && options.length) {
      event.preventDefault();
      setOpen(true);
      setActive((event.key === "ArrowDown" ? current + 1 : current <= 0 ? options.length - 1 : current - 1) % options.length);
    }
    if (event.key === "Tab" && !event.shiftKey && visible && current >= 0) {
      choose(options[current]);
    } else if (event.key === "Enter" && visible && (current >= 0 || onChoose)) {
      event.preventDefault();
      choose(options[Math.max(0, current)]);
    } else if (event.key === "Enter" && !multiline) onSubmit?.();
  };
  const inputProps = {
    ref: (element: HTMLInputElement | HTMLTextAreaElement | null) => { inputElement.current = element; },
    role: "combobox" as const,
    "aria-label": label,
    "aria-autocomplete": "list" as const,
    "aria-expanded": visible,
    "aria-controls": visible ? id : undefined,
    "aria-activedescendant": visible && current >= 0 ? id + "-" + current : undefined,
    placeholder: placeholder ?? label,
    value,
    readOnly: disabled,
    onBlur: () => { setOpen(false); setActive(-1); },
    onSelect: (event: SyntheticEvent<HTMLInputElement | HTMLTextAreaElement>) => onCaret?.(event.currentTarget.selectionStart ?? 0),
    onChange: handleChange,
    onKeyDown: handleKeyDown,
  };
  return (
    <div className={`relative ${className ?? ""}`}>
      <span className="table-query-label">{label}</span>
      {multiline ? <textarea {...inputProps} rows={1} /> : <input {...inputProps} />}
      {visible && <ul id={id} role="listbox" className="table-suggestions" tabIndex={-1}>
        {options.map((option, index) => (
          <li key={option.value + "-" + index} id={id + "-" + index} role="option" aria-selected={index === current}
            onPointerDown={(event) => event.preventDefault()} onClick={() => choose(option)}>
            {option.label}
          </li>
        ))}
      </ul>}
    </div>
  );
}

function TableToolbarPortal({ slotId, children }: { slotId?: string; children: ReactNode }) {
  const [host, setHost] = useState<HTMLElement | null>(null);
  useEffect(() => {
    setHost(slotId ? document.getElementById(slotId) : null);
  }, [slotId]);
  return host ? createPortal(children, host) : null;
}

export function TableTool({ scope, target, targets, initialTable, toolbarSlotId, visible, onTargetChange, onText, onConnect, onTableChange }: {
  scope: string;
  target?: Target;
  targets: Target[];
  initialTable?: string;
  toolbarSlotId?: string;
  visible: boolean;
  onTargetChange: (target: Target) => void;
  onText: (text: string, name: string) => void;
  onConnect: () => void;
  onTableChange: (table: string) => void;
}) {
  const [input, setInput] = useState(initialTable ?? ""), [where, setWhere] = useState(""),
    [columns, setColumns] = useState("*"),
    [readonly, setReadonly] = useState(false), [selectedColumns, setSelectedColumns] = useState<string[]>([]),
    [columnSearch, setColumnSearch] = useState(""), [filterField, setFilterField] = useState(""),
    [filterOperator, setFilterOperator] = useState("LIKE"), [filterValue, setFilterValue] = useState(""),
    [mode, setMode] = useState<"data" | "structure">("data"),
    [catalog, setCatalog] = useState<{ targetId: string; tables: TableName[] }>(),
    [catalogRevision, setCatalogRevision] = useState(0),
    [quickCatalog, setQuickCatalog] = useState<{ targetId: string; query: string; tables: TableName[] }>(),
    [catalogError, setCatalogError] = useState(""),
    [carets, setCarets] = useState<Record<Clause, number>>({ select: 0, where: 0 }),
    [structure, setStructure] = useState<{ key: string; data: Table }>(),
    [metadata, setMetadata] = useState<{ key: string; columns: TableColumn[] }>(),
    [metadataRevision, setMetadataRevision] = useState(0),
    [preview, setPreview] = useState<{ key: string; result: Preview }>(),
    [selectedRows, setSelectedRows] = useState<number[]>([]),
    [menu, setMenu] = useState<ColumnMenu>(),
    [columnMenu, setColumnMenu] = useState<ColumnMenu>(),
    [drop, setDrop] = useState<string[]>(),
    [scriptPreview, setScriptPreview] = useState<ScriptPreview>(),
    [columnDialog, setColumnDialog] = useState<ColumnDialog>(),
    [columnMenuGroup, setColumnMenuGroup] = useState<string>(),
    [scriptMenu, setScriptMenu] = useState(false),
    [status, setStatus] = useState(""),
    [dataError, setDataError] = useState("");
  const task = useTask();
  const [previewPending, startPreviewTransition] = useTransition();
  const reading = useRef<ReturnType<typeof operation<Preview>>>();
  const [showQueryCancel, setShowQueryCancel] = useState(false);
  const autoReadKey = useRef("");
  const contextMenu = useRef<HTMLDivElement>(null);
  const columnContextMenu = useRef<HTMLDivElement>(null);
  const scriptControls = useRef<HTMLDivElement>(null);
  const name = parseTable(input);
  const targetId = target?.id;
  const reportedTable = useRef(initialTable ?? "");
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
  const currentStructure = structure?.key === key && isTable(structure.data) ? structure.data : undefined;
  const currentMetadata = metadata?.key === key && Array.isArray(metadata.columns) ? metadata.columns : undefined;
  const currentPreview = preview?.key === key ? preview.result : undefined;
  const fieldNames = currentMetadata?.map((column) => column.name)
    ?? currentStructure?.rows.flatMap((row) => row[0] ? [row[0]] : []) ?? [];
  const filteredMetadata = (currentMetadata ?? []).filter((column) =>
    column.name.toLowerCase().includes(columnSearch.trim().toLowerCase()));
  const payload = { targetId, schema, table };
  const canRead = !!targetId && !!name && !task.busy;
  useEffect(() => {
    reportedTable.current = initialTable ?? "";
  }, [initialTable, targetId]);
  useEffect(() => {
    if (!task.busy) {
      setShowQueryCancel(false);
      return;
    }
    const timer = window.setTimeout(() => setShowQueryCancel(true), 250);
    return () => window.clearTimeout(timer);
  }, [task.busy]);
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
    setStructure(undefined);
    setMetadata(undefined);
    setPreview(undefined);
    setDataError("");
    autoReadKey.current = "";
    setWhere("");
    setColumns("*");
    setSelectedColumns([]);
    setFilterField("");
    setFilterValue("");
    setCarets({ select: 0, where: 0 });
    setScriptMenu(false);
  }, [targetId]);
  useEffect(() => {
    if (initialTable && initialTable !== input) setInput(initialTable);
  }, [initialTable]);
  useEffect(() => {
    setSelectedRows([]);
    setMenu(undefined);
    setColumnMenu(undefined);
    setColumnMenuGroup(undefined);
    setDrop(undefined);
    setScriptPreview(undefined);
    setStatus("");
    setDataError("");
    if (!targetId || !schema || !table || !knownTable) return;
    let current = true;
    let pending: ReturnType<typeof operation<Table>> | undefined;
    const timer = window.setTimeout(() => {
      pending = operation<Table>("table.structure", { targetId, schema, table });
      void pending.promise.then((data) => {
        if (!current) return;
        setStructure({ key, data: normalizeTable(data, "cấu trúc bảng") });
        setDataError("");
      }).catch((error) => { if (current) setDataError((error as Error).message); });
    }, 80);
    return () => {
      current = false;
      window.clearTimeout(timer);
      void pending?.cancel().catch(() => {});
    };
  }, [key, targetId, schema, table, knownTable]);
  useEffect(() => {
    if (!targetId || !schema || !table || !knownTable) return;
    let current = true;
    let pending: ReturnType<typeof operation<{ columns?: unknown }>> | undefined;
    const timer = window.setTimeout(() => {
      pending = operation<{ columns?: unknown }>("table.metadata", { targetId, schema, table });
      void pending.promise.then((result) => {
        if (!current) return;
        const columns = normalizeMetadata(result.columns);
        setMetadata({ key, columns });
        setSelectedColumns((old) => old.filter((name) => columns.some((column) => column.name.toLowerCase() === name.toLowerCase())));
        setDataError("");
      }).catch((error) => { if (current) setDataError((error as Error).message); });
    }, 80);
    return () => {
      current = false;
      window.clearTimeout(timer);
      void pending?.cancel().catch(() => {});
    };
  }, [key, targetId, schema, table, knownTable, metadataRevision]);
  useEffect(() => () => {
    const pending = reading.current;
    if (pending) void pending.cancel().catch(() => {});
    void (pending?.promise.catch(() => {}) ?? Promise.resolve())
      .then(() => request("table.releaseSnapshot", { scope })).catch(() => {});
  }, [scope]);
  useEffect(() => {
    if (!menu && !columnMenu && !scriptMenu) return;
    function dismiss(event: PointerEvent) {
      const element = event.target as Node;
      if (!contextMenu.current?.contains(element)) setMenu(undefined);
      if (!columnContextMenu.current?.contains(element)) { setColumnMenu(undefined); setColumnMenuGroup(undefined); }
      if (!scriptControls.current?.contains(element)) setScriptMenu(false);
    }
    function escape(event: globalThis.KeyboardEvent) {
      if (event.key === "Escape") { setMenu(undefined); setColumnMenu(undefined); setColumnMenuGroup(undefined); setScriptMenu(false); }
    }
    document.addEventListener("pointerdown", dismiss);
    document.addEventListener("keydown", escape);
    if (menu) contextMenu.current?.querySelector("button")?.focus();
    return () => {
      document.removeEventListener("pointerdown", dismiss);
      document.removeEventListener("keydown", escape);
    };
  }, [menu, columnMenu, scriptMenu]);

  const reportTableChange = (chosen: TableName) => {
    const next = tableIdentity(chosen);
    if (!next || reportedTable.current === next) return;
    reportedTable.current = next;
    onTableChange(next);
  };
  const readStructure = async (chosen = name) => {
    if (!chosen || !targetId) return;
    const data = normalizeTable(await request<unknown>("table.structure", { targetId, ...chosen }), "cấu trúc bảng");
    reportTableChange(chosen);
    setStructure({ key: JSON.stringify([targetId, chosen.schema, chosen.table]), data });
    setSelectedRows([]);
  };
  const generatedWhere = () => {
    if (!filterField || !filterOperator) return "";
    const field = "[" + filterField.replaceAll("]", "]]" ) + "]";
    if (filterOperator === "IS NULL" || filterOperator === "IS NOT NULL") return field + " " + filterOperator;
    const value = filterValue.replaceAll("'", "''");
    if (!value) return "";
    const pattern = filterOperator === "LIKE" || filterOperator === "NOT LIKE" ? "%" + value + "%" : value;
    return field + " " + filterOperator + " N'" + pattern + "'";
  };
  const readDataRequest = async (chosen = name, filters = { where: [where.trim(), generatedWhere()].filter(Boolean).join(" AND "), select: columns || "*" }) => {
    if (!chosen || !targetId) return;
    const op = operation<Preview>("table.view", { scope, targetId, ...chosen, ...filters });
    reading.current = op;
    try {
      const result = normalizePreview(await op.promise);
      startPreviewTransition(() => {
        reportTableChange(chosen);
        setPreview({ key: JSON.stringify([targetId, chosen.schema, chosen.table]), result });
        setDataError("");
      });
    }
    finally { if (reading.current === op) reading.current = undefined; }
  };
  const readData = (chosen = name, filters = { where: [where.trim(), generatedWhere()].filter(Boolean).join(" AND "), select: columns || "*" }) => task.run(() => readDataRequest(chosen, filters));
  useEffect(() => {
    const chosen = initialTable ? parseTable(initialTable) : undefined;
    if (!visible || !chosen || !knownTable || !targetId || input !== initialTable || preview?.key === key || autoReadKey.current === key) return;
    autoReadKey.current = key;
    void readData(chosen, { where: "", select: "*" });
    // Parent stores the qualified name after the first read.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [initialTable, input, visible, knownTable, targetId, key]);
  function chooseTable(value: string) {
    const chosen = parseTable(value);
    if (!chosen) return;
    const changing = chosen.schema !== schema || chosen.table !== table;
    if (changing) {
      setWhere("");
      setColumns("*");
      setSelectedColumns([]);
      setFilterField("");
      setFilterValue("");
      setCarets({ select: 0, where: 0 });
    }
    if (mode === "data") void readData(chosen, changing ? { where: "", select: "*" } : undefined);
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
  const scriptActionLabels: Record<string, string> = {
    add: "Thêm cột",
    alter: "Sửa cột",
    drop: "Xóa cột",
    create: "Tạo bảng",
    log: "Nhật ký cột",
    addmanual: "Thêm cột",
    altermanual: "Sửa cột",
  };
  async function runScript(action: string, output: "view" | "copy" | "open", columnsOverride = selectedColumns) {
    await task.run(async () => {
      if (!schema || !table || !targetId) return;
      const script = await request<string>("table.script", {
        ...payload,
        action,
        columns: columnsOverride,
      });
      if (output === "view") setScriptPreview({ title: "SQL · " + (scriptActionLabels[action] ?? action), script });
      else if (output === "copy") {
        await writeClipboard(script);
        setStatus("Đã sao chép lệnh SQL.");
      } else {
        onText(script, table + "-" + action + ".sql");
      }
    });
  }
  function commitRow(rowIndex: number, values: (string | null)[]) {
    if (readonly || !currentPreview || !currentMetadata || !name || !targetId) return;
    void task.run(async () => {
      const visibleColumns = currentPreview.data.columns;
      if (rowIndex >= 0) {
        const original = currentPreview.data.rows[rowIndex];
        if (!original) return;
        const primary = currentMetadata.filter((column) => column.primaryKey);
        if (!primary.length || primary.some((column) => !visibleColumns.some((name) => name.toLowerCase() === column.name.toLowerCase())))
          throw new Error("Bảng không có đủ khóa chính trong kết quả để cập nhật dòng.");
        const changes = visibleColumns.map((column, index) => ({
          column,
          value: values[index] ?? null,
          old: original[index] ?? null,
        })).filter((entry) => entry.value !== entry.old && currentMetadata.some((meta) =>
          meta.name.toLowerCase() === entry.column.toLowerCase() && meta.writable));
        if (!changes.length) return;
        const keyEntries = primary.map((column) => {
          const index = visibleColumns.findIndex((name) => name.toLowerCase() === column.name.toLowerCase());
          return { name: column.name, value: original[index] ?? null };
        });
        await request("table.write", {
          ...payload,
          mode: "update",
          columns: changes.map((entry) => entry.column),
          values: changes.map((entry) => entry.value),
          keyColumns: keyEntries.map((entry) => entry.name),
          keyValues: keyEntries.map((entry) => entry.value),
        });
      } else {
        if (values.every((value) => value == null || value === "")) return;
        await request("table.write", { ...payload, mode: "insert", columns: visibleColumns, values });
      }
      setPreview(undefined);
       await readDataRequest();
      setStatus(rowIndex < 0 ? "Đã thêm dòng dữ liệu." : "Đã cập nhật dòng dữ liệu.");
    });
  }
  function selectColumns(names: string[]) {
    setSelectedColumns(names);
    setColumns(names.length ? names.map((name) => "[" + name.replaceAll("]", "]]" ) + "]").join(", ") : "*");
  }
  const view = mode === "structure" ? currentStructure : currentPreview?.data;
  const filterOptions = [
    { value: "", label: "Chọn trường" },
    ...fieldNames.map((field) => ({ value: field, label: field })),
  ];
  const operatorOptions = ["LIKE", "=", "<>", ">", ">=", "<", "<=", "IS NULL", "IS NOT NULL", "NOT LIKE"]
    .map((operator) => ({ value: operator, label: operator }));
  const scriptGroups: { action: string; label: string }[] = [
    { action: "add", label: "Tạo lệnh thêm cột" },
    { action: "alter", label: "Tạo lệnh sửa cột" },
    { action: "drop", label: "Tạo lệnh xóa cột" },
    { action: "create", label: "Tạo lệnh tạo bảng" },
    { action: "log", label: "Tạo lệnh nhật ký cột" },
  ];
  const scriptOutputs: { output: "view" | "copy" | "open"; label: string }[] = [
    { output: "view", label: "Xem lệnh SQL" },
    { output: "copy", label: "Sao chép" },
    { output: "open", label: "Mở thành tab SQL" },
  ];
  const toolbarControls = <div className="table-toolbar-controls">
    {targets.length > 0 && <Select label="Cơ sở dữ liệu" value={targetId ?? ""} disabled={task.busy || !!drop}
      options={targets.map((entry) => ({
        value: entry.id,
        label: (/_S(?:ys)?$/i.test(entry.database) ? "SYS · " : /_A(?:pp)?$/i.test(entry.database) ? "Ứng dụng · " : "") + entry.database,
      }))}
      onChange={(id) => {
        const next = targets.find((entry) => entry.id === id);
        if (next && next.id !== targetId) onTargetChange(next);
      }} />}
    {targetId && <IconButton label="Tải lại danh sách bảng" onClick={() => {
      tableCatalogCache.delete(targetId);
      setQuickCatalog(undefined);
      setCatalogRevision((version) => version + 1);
    }}><RefreshCw size={15} /></IconButton>}
    <Button aria-pressed={mode === "data"} variant={mode === "data" ? "secondary" : "ghost"} onClick={() => setMode("data")}>Dữ liệu</Button>
    <Button aria-pressed={mode === "structure"} variant={mode === "structure" ? "secondary" : "ghost"} onClick={() => setMode("structure")}>Cấu trúc</Button>
    <div ref={scriptControls} className="relative">
      <Button aria-expanded={scriptMenu} disabled={!canRead} onClick={() => setScriptMenu(!scriptMenu)}>Tập lệnh</Button>
      {scriptMenu && <div className="table-action-menu absolute top-full right-0 z-10" aria-label="Loại lệnh SQL">
        <Button variant="ghost" onClick={() => generateScript("create")}>Tạo lệnh tạo bảng</Button>
        <Button variant="ghost" disabled={mode !== "data" || !currentPreview?.data.rows.length} onClick={() => generateScript("insert")}>Tạo lệnh thêm dữ liệu đang hiển thị</Button>
      </div>}
    </div>
    {target ? <span className="table-toolbar-row-count muted" title={target.server + " / " + target.database}>{view?.rows.length ?? 0} dòng</span>
      : <Button onClick={onConnect}>Kết nối SQL</Button>}
  </div>;
  return (
    <div className="table-inspection flex min-h-0 min-w-0 flex-1 flex-col gap-2 overflow-hidden p-3">
      <div className="table-query-panel">
        <div className="table-query-row">
          <SuggestedInput label="SELECT" placeholder="*" value={columns}
            options={clauseSuggestions(columns, Math.min(carets.select, columns.length), fieldNames, "select")}
            onCaret={(position) => setCarets((old) => old.select === position ? old : { ...old, select: position })}
            onChange={(value) => { setColumns(value); setSelectedColumns([]); }} disabled={false} className="table-query-select"
            onSubmit={() => { if (canRead) void readData(); }} />
          <SuggestedInput label="FROM" placeholder="Tên bảng hoặc schema.bảng" options={names} value={input}
            onChange={setInput} onChoose={chooseTable} disabled={task.busy} focus={visible} className="table-query-from"
            onSubmit={() => { if (canRead) void readData(); }} />
          <label className="table-readonly"><input type="checkbox" checked={readonly} onChange={(event) => setReadonly(event.target.checked)} /> Readonly</label>
          <Button variant="primary" disabled={!canRead} onClick={() => void (mode === "data" ? readData() : task.run(readStructure))}>
            <Play size={14} />Chạy
          </Button>
          <span className="table-query-cancel-slot">
            {showQueryCancel && reading.current && <Button onClick={() => void reading.current?.cancel()}>Dừng</Button>}
          </span>
        </div>
        <SuggestedInput label="WHERE" placeholder="Điều kiện WHERE (không bắt buộc)" value={where} multiline
          options={clauseSuggestions(where, Math.min(carets.where, where.length), fieldNames, "where")}
          onCaret={(position) => setCarets((old) => old.where === position ? old : { ...old, where: position })}
          onChange={setWhere} disabled={false} className="table-query-where" />
      </div>
      <TableToolbarPortal slotId={toolbarSlotId}>{toolbarControls}</TableToolbarPortal>
      {targetId && (!tables || !names.length) && <p className={catalogError ? "form-error" : "muted"} role={catalogError ? "alert" : "status"}>
        {catalogError || (tables ? "Không tìm thấy bảng phù hợp." : "Đang tải danh sách bảng…")}
      </p>}
      {task.error && <p className="form-error" role="alert">{task.error}</p>}
      {dataError && <p className="form-error" role="alert">{dataError}</p>}
      {status && <span className="muted" role="status">{status}</span>}
      <div className="table-inspection-body">
        <aside className="table-column-panel" aria-label="Các trường của bảng">
          <div className="table-column-panel-heading">
            <input aria-label="Tìm trường" placeholder="Tìm trường…" value={columnSearch} onChange={(event) => setColumnSearch(event.target.value)} />
            <div className="table-column-panel-actions">
              <Button variant="ghost" disabled={!currentMetadata?.length} onClick={() => selectColumns(currentMetadata?.map((column) => column.name) ?? [])}>Chọn tất cả</Button>
              <Button variant="ghost" disabled={!currentMetadata?.length} onClick={() => selectColumns([])}>Bỏ chọn</Button>
            </div>
          </div>
          <div className="table-column-options">
          {filteredMetadata.length ? filteredMetadata.map((column) => {
            const checked = selectedColumns.some((name) => name.toLowerCase() === column.name.toLowerCase());
            return <label key={column.name} className={`table-column-option ${column.key ? "table-key-column" : ""}`} onContextMenu={(event) => {
              event.preventDefault();
              const nextColumns = checked ? selectedColumns : [...selectedColumns, column.name];
              if (!checked) selectColumns(nextColumns);
              setColumnMenuGroup(undefined);
              setColumnMenu({
                x: Math.max(8, Math.min(event.clientX, innerWidth - 250)),
                y: Math.max(8, Math.min(event.clientY, innerHeight - 300)),
                columns: nextColumns,
              });
            }}>
              <input type="checkbox" checked={checked} onChange={(event) => selectColumns(event.target.checked
                ? [...selectedColumns, column.name]
                : selectedColumns.filter((name) => name.toLowerCase() !== column.name.toLowerCase()))} />
              <span className="table-column-key-slot">
                {column.key && <KeyRound size={13} aria-label={column.primaryKey ? "Khóa chính" : "Khóa"} />}
              </span>
              <span>{column.name}</span>
              <small>{column.sqlType}</small>
            </label>;
          }) : <span className="muted table-column-empty">{currentMetadata ? "Không có trường phù hợp." : "Chọn bảng để tải danh sách trường."}</span>}
          </div>
        </aside>
        <section className="table-inspection-content">
          {mode === "data" && currentPreview?.data && currentMetadata ? <TableDataGrid
            table={currentPreview.data} metadata={currentMetadata} readonly={readonly} busy={task.busy || previewPending} onCommit={commitRow}
            emptyMessage={filterField && (filterOperator === "IS NULL" || filterOperator === "IS NOT NULL" || filterValue.trim())
              ? "Không tìm thấy dữ liệu phù hợp."
              : undefined} />
            : view ? <DataGrid key={mode + key} table={view} rangeSelection
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
              : <div className="empty">{task.busy ? "Đang đọc bảng…" : "Chọn bảng để xem cấu trúc hoặc đọc tối đa 1.000 dòng dữ liệu."}</div>}
        </section>
      </div>
      {currentMetadata && <div className="table-filter-row">
        <Select floating label="Trường lọc" value={filterField} options={filterOptions} onChange={setFilterField} disabled={!fieldNames.length} />
        <Select floating label="Toán tử" value={filterOperator} options={operatorOptions} onChange={setFilterOperator} disabled={!filterField} />
        <input aria-label="Giá trị lọc" placeholder="Giá trị" value={filterValue} onChange={(event) => setFilterValue(event.target.value)} disabled={!filterField || filterOperator === "IS NULL" || filterOperator === "IS NOT NULL"} />
        <Button variant="secondary" disabled={!canRead || !filterField} onClick={() => void readData()}>Lọc</Button>
      </div>}
      {menu && (
        <div ref={contextMenu} className="table-action-menu fixed z-20" style={{ left: menu.x, top: menu.y }} aria-label="Thao tác cột">
          {([["addScript", "THÊM CỘT"], ["alterScript", "SỬA CỘT"]] as const).map(([method, label]) =>
            <Button key={method} variant="ghost" onClick={() => {
              setMenu(undefined);
              void task.run(async () => {
                const script = await request<string>("table." + method, { ...payload, columns: menu.columns });
                await writeClipboard(script);
                setStatus("Đã sao chép lệnh " + label + ".");
              });
            }}>Tạo lệnh {label}</Button>)}
          <Button variant="ghost" className="table-drop-action" onClick={() => {
            setMenu(undefined); setDrop(menu.columns);
          }}>Xóa cột</Button>
        </div>
      )}
      {columnMenu && <div ref={columnContextMenu} className="table-action-menu table-column-context-menu fixed z-20" style={{ left: columnMenu.x, top: columnMenu.y }} aria-label="Thao tác trường">
        {scriptGroups.map((group) => <div key={group.action} className="table-context-group">
          <Button variant="ghost" onClick={() => setColumnMenuGroup(columnMenuGroup === group.action ? undefined : group.action)}>
            {group.label}<ChevronRight size={14} />
          </Button>
          {columnMenuGroup === group.action && <div className="table-context-submenu">
            {scriptOutputs.map((item) => <Button key={item.output} variant="ghost" onClick={() => {
              const columnsForScript = columnMenu.columns;
              setColumnMenu(undefined);
              setColumnMenuGroup(undefined);
              void runScript(group.action, item.output, columnsForScript);
            }}>{item.label}</Button>)}
          </div>}
        </div>)}
        <div className="table-context-group">
          <Button variant="ghost" onClick={() => setColumnMenuGroup(columnMenuGroup === "edit" ? undefined : "edit")}>Sửa cột<ChevronRight size={14} /></Button>
          {columnMenuGroup === "edit" && <div className="table-context-submenu">
            <Button variant="ghost" onClick={() => { setColumnDialog({ mode: "add" }); setColumnMenu(undefined); setColumnMenuGroup(undefined); }}>Thêm</Button>
            <Button variant="ghost" disabled={columnMenu.columns.length !== 1} onClick={() => { setColumnDialog({ mode: "alter", column: columnMenu.columns[0] }); setColumnMenu(undefined); setColumnMenuGroup(undefined); }}>Sửa</Button>
          </div>}
        </div>
        <Button variant="ghost" onClick={() => selectColumns(currentMetadata?.map((column) => column.name) ?? [])}>Chọn tất cả<kbd>Ctrl+A</kbd></Button>
        <Button variant="ghost" onClick={() => selectColumns([])}>Bỏ chọn<kbd>Ctrl+Shift+U</kbd></Button>
        <Button variant="ghost" onClick={() => {
          setColumnMenu(undefined);
          setColumnMenuGroup(undefined);
          setMetadata(undefined);
          setMetadataRevision((version) => version + 1);
          void (mode === "data" ? readData() : task.run(readStructure));
        }}>Tải lại</Button>
      </div>}
      {scriptPreview && <TableScriptDialog title={scriptPreview.title} script={scriptPreview.script}
        onClose={() => setScriptPreview(undefined)}
        onCopy={() => void writeClipboard(scriptPreview.script)}
        onOpen={() => { onText(scriptPreview.script, (table ?? "table") + "-script.sql"); setScriptPreview(undefined); }} />}
      {columnDialog && <TableColumnDialog mode={columnDialog.mode} column={columnDialog.column} busy={task.busy}
        onClose={() => setColumnDialog(undefined)}
        onSubmit={(columnName, dataType, nullable) => {
          const current = columnDialog;
          setColumnDialog(undefined);
          void task.run(async () => {
            if (!schema || !table || !targetId) return;
            const action = current.mode === "add" ? "addmanual" : "altermanual";
            const script = await request<string>("table.script", {
              ...payload, action, column: columnName, dataType, nullable,
              columns: current.column ? [current.column] : [],
            });
            setScriptPreview({ title: "SQL · " + (scriptActionLabels[action] ?? action), script });
          });
        }} />}
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
                setMetadata(undefined);
                setMetadataRevision((version) => version + 1);
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

type TableToolErrorBoundaryProps = { children: ReactNode };
type TableToolErrorBoundaryState = { error?: Error };

export class TableToolErrorBoundary extends Component<TableToolErrorBoundaryProps, TableToolErrorBoundaryState> {
  state: TableToolErrorBoundaryState = {};

  static getDerivedStateFromError(error: Error): TableToolErrorBoundaryState {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error("TableTool render failed", error, info);
  }

  render() {
    if (!this.state.error) return this.props.children;
    return <div className="empty" role="alert">
      <strong>Không thể hiển thị bảng.</strong>
      <pre>{this.state.error.message}</pre>
      <span>Hãy chọn lại bảng hoặc nhấn Chạy để thử lại.</span>
    </div>;
  }
}
