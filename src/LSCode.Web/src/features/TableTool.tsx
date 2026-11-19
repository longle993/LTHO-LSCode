import { Component, useEffect, useId, useLayoutEffect, useMemo, useRef, useState, useTransition } from "react";
import type { ChangeEvent, ErrorInfo, KeyboardEvent as ReactKeyboardEvent, ReactNode, SyntheticEvent } from "react";
import { createPortal } from "react-dom";
import { ChevronRight, CircleAlert, Copy, KeyRound, Play, RefreshCw, X } from "lucide-react";
import { Button, IconButton, Modal, Select, WorkbenchNotice } from "../ui";
import { operation, quoteSqlName, request, writeClipboard } from "../bridge";
import type { Table, Target } from "../bridge";
import { useTask } from "./useTask";
import { TableDataGrid, type TableColumn } from "./TableDataGrid";
import { TableColumnDialog } from "./TableColumnDialog";
import { TableScriptDialog } from "./TableScriptDialog";

type TableName = { schema: string; table: string };
const tableCatalogCache = new Map<string, { tables: TableName[]; expires: number }>();
const tableCatalogChanged = "lscode:table-catalog-changed";
export function invalidateTableCatalog(targetId: string) {
  tableCatalogCache.delete(targetId);
  window.dispatchEvent(new CustomEvent(tableCatalogChanged, { detail: targetId }));
}
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
type DeleteRequest = { count: number; table: string; resolve: (confirmed: boolean) => void };
type Suggestion = { label: string; value: string; caret?: number };
type Clause = "select" | "where" | "orderBy";
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
  orderBy: ["ASC", "DESC"],
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

function SuggestedInput({ label, placeholder, value, options, disabled, focus, autoFocus, onChange, onChoose, onSubmit, onCaret, className, multiline = false }: {
  label: string; placeholder?: string; value: string; options: Suggestion[]; disabled: boolean;
  focus?: boolean;
  autoFocus?: boolean;
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
    ref: (element: HTMLInputElement | HTMLTextAreaElement | null) => {
      inputElement.current = element;
      if (element) element.autofocus = autoFocus ?? false;
    },
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

export function TableTool({ scope, target, targets, initialTable, toolbarSlotId, visible, onTargetChange, onText, onConnect, onTableChange, onCaptureData }: {
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
  onCaptureData: (target: Target, table: string, where: string) => void;
}) {
  const [input, setInput] = useState(initialTable ?? ""), [where, setWhere] = useState(""), [orderBy, setOrderBy] = useState(""),
    [columns, setColumns] = useState("*"),
    [readonly, setReadonly] = useState(false), [selectedColumns, setSelectedColumns] = useState<string[]>([]),
    [columnSearch, setColumnSearch] = useState(""), [filterField, setFilterField] = useState(""),
    [filterOperator, setFilterOperator] = useState("LIKE"), [filterValue, setFilterValue] = useState(""),
    [catalog, setCatalog] = useState<{ targetId: string; tables: TableName[] }>(),
    [catalogRevision, setCatalogRevision] = useState(0),
    [quickCatalog, setQuickCatalog] = useState<{ targetId: string; query: string; tables: TableName[] }>(),
    [catalogError, setCatalogError] = useState(""),
    [carets, setCarets] = useState<Record<Clause, number>>({ select: 0, where: 0, orderBy: 0 }),
    [metadata, setMetadata] = useState<{ key: string; columns: TableColumn[] }>(),
    [metadataRevision, setMetadataRevision] = useState(0),
    [preview, setPreview] = useState<{ key: string; result: Preview }>(),
    [columnMenu, setColumnMenu] = useState<ColumnMenu>(),
    [drop, setDrop] = useState<string[]>(),
    [scriptPreview, setScriptPreview] = useState<ScriptPreview>(),
    [columnDialog, setColumnDialog] = useState<ColumnDialog>(),
    [columnMenuGroup, setColumnMenuGroup] = useState<string>(),
    [scriptMenu, setScriptMenu] = useState(false),
    [status, setStatus] = useState(""),
    [dataError, setDataError] = useState(""),
    [goToOpen, setGoToOpen] = useState(false),
    [goToField, setGoToField] = useState(""),
    [goToRequest, setGoToRequest] = useState<{ name: string; sequence: number }>(),
    [deleteRequest, setDeleteRequest] = useState<DeleteRequest>();
  const task = useTask();
  const [saveSlot, setSaveSlot] = useState<HTMLSpanElement | null>(null);
  const [previewPending, startPreviewTransition] = useTransition();
  const reading = useRef<ReturnType<typeof operation<Preview>>>();
  const [showQueryCancel, setShowQueryCancel] = useState(false);
  const autoReadKey = useRef("");
  const columnContextMenu = useRef<HTMLDivElement>(null);
  const scriptControls = useRef<HTMLDivElement>(null);
  const scriptPopup = useRef<HTMLDivElement>(null);
  const name = parseTable(input);
  const targetId = target?.id;
  const reportedTable = useRef(initialTable ?? "");
  const schema = name?.schema, table = name?.table;
  const key = JSON.stringify([targetId, schema, table]);
  const tables = catalog?.targetId === targetId ? catalog?.tables : undefined;
  const query = input.replace(/[\[\]]/g, "").trim().toLowerCase();
  const matchesTable = (entry: TableName) =>
    entry.schema.toLowerCase() === schema?.toLowerCase() && entry.table.toLowerCase() === table?.toLowerCase();
  const listedTable = !!tables?.some(matchesTable);
  const quickTables = quickCatalog?.targetId === targetId && quickCatalog?.query === query ? quickCatalog.tables : undefined;
  const candidates = useMemo(() => {
    if (!tables || !quickTables) return tables ?? quickTables;
    return [...new Map([...tables, ...quickTables].map((entry) => [quoteSqlName(entry.schema, entry.table), entry])).values()];
  }, [tables, quickTables]);
  const knownTable = listedTable || !!quickTables?.some(matchesTable);
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
  const currentMetadata = metadata?.key === key && Array.isArray(metadata.columns) ? metadata.columns : undefined;
  const currentPreview = preview?.key === key ? preview.result : undefined;
  const canReadMetadata = knownTable || !!currentPreview;
  const fieldNames = currentMetadata?.map((column) => column.name) ?? [];
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
    if (!status || task.error || dataError || catalogError) return;
    const timer = window.setTimeout(() => setStatus(""), 4200);
    return () => window.clearTimeout(timer);
  }, [status, task.error, dataError, catalogError]);
  useLayoutEffect(() => {
    function invalidate(event: Event) {
      if ((event as CustomEvent<string>).detail !== targetId) return;
      setQuickCatalog(undefined);
      setCatalogRevision((version) => version + 1);
      setMetadataRevision((version) => version + 1);
    }
    window.addEventListener(tableCatalogChanged, invalidate);
    return () => window.removeEventListener(tableCatalogChanged, invalidate);
  }, [targetId]);
  useEffect(() => {
    if (!visible) return;
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
  }, [targetId, catalogRevision, visible]);
  useEffect(() => {
    if (!visible || !targetId || (tables && (!query || listedTable))) return;
    const cached = tableCatalogCache.get(targetId);
    if (!tables && cached && cached.expires > Date.now()) return;
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
  }, [targetId, tables, query, listedTable, catalogRevision, visible]);
  useEffect(() => {
    setMetadata(undefined);
    setPreview(undefined);
    setDataError("");
    autoReadKey.current = "";
    setWhere("");
    setOrderBy("");
    setColumns("*");
    setSelectedColumns([]);
    setFilterField("");
    setFilterValue("");
    setCarets({ select: 0, where: 0, orderBy: 0 });
    setScriptMenu(false);
  }, [targetId]);
  useEffect(() => {
    if (initialTable && initialTable !== input) setInput(initialTable);
  }, [initialTable]);
  useEffect(() => {
    setColumnMenu(undefined);
    setColumnMenuGroup(undefined);
    setDrop(undefined);
    setScriptPreview(undefined);
    setStatus("");
    setDataError("");
  }, [key]);
  useEffect(() => {
    if (!visible || !targetId || !schema || !table || !canReadMetadata) return;
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
  }, [key, targetId, schema, table, canReadMetadata, metadataRevision, visible]);
  useEffect(() => () => {
    const pending = reading.current;
    if (pending) void pending.cancel().catch(() => {});
    void (pending?.promise.catch(() => {}) ?? Promise.resolve())
      .then(() => request("table.releaseSnapshot", { scope })).catch(() => {});
  }, [scope]);
  useEffect(() => {
    if (!columnMenu && !scriptMenu) return;
    function dismiss(event: PointerEvent) {
      const element = event.target as Node;
      if (!columnContextMenu.current?.contains(element)) { setColumnMenu(undefined); setColumnMenuGroup(undefined); }
      if (!scriptControls.current?.contains(element) && !scriptPopup.current?.contains(element)) setScriptMenu(false);
    }
    function escape(event: globalThis.KeyboardEvent) {
      if (event.key === "Escape") { setColumnMenu(undefined); setColumnMenuGroup(undefined); setScriptMenu(false); }
    }
    document.addEventListener("pointerdown", dismiss);
    document.addEventListener("keydown", escape);
    return () => {
      document.removeEventListener("pointerdown", dismiss);
      document.removeEventListener("keydown", escape);
    };
  }, [columnMenu, scriptMenu]);
  useLayoutEffect(() => {
    const popup = scriptPopup.current;
    const trigger = scriptControls.current?.querySelector<HTMLButtonElement>("button");
    if (!scriptMenu || !popup || !trigger) return;
    popup.setAttribute("popover", "manual");
    if (typeof popup.showPopover === "function") popup.showPopover();
    const place = () => {
      const rect = trigger.getBoundingClientRect();
      const width = Math.min(250, window.innerWidth - 16);
      const below = window.innerHeight - rect.bottom - 8;
      const above = rect.top - 8;
      const upward = below < 150 && above > below;
      Object.assign(popup.style, {
        width: `${width}px`,
        left: `${Math.max(8, Math.min(rect.right - width, window.innerWidth - width - 8))}px`,
        top: upward ? "auto" : `${rect.bottom + 4}px`,
        bottom: upward ? `${window.innerHeight - rect.top + 4}px` : "auto",
        maxHeight: `${Math.max(80, Math.min(220, upward ? above : below))}px`,
      });
    };
    place();
    window.addEventListener("resize", place);
    document.addEventListener("scroll", place, true);
    return () => {
      window.removeEventListener("resize", place);
      document.removeEventListener("scroll", place, true);
      if (popup.matches(":popover-open")) popup.hidePopover();
    };
  }, [scriptMenu]);

  const reportTableChange = (chosen: TableName) => {
    const next = tableIdentity(chosen);
    if (!next || reportedTable.current === next) return;
    reportedTable.current = next;
    onTableChange(next);
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
  const readDataRequest = async (chosen = name, filters = { where: [where.trim(), generatedWhere()].filter(Boolean).join(" AND "), select: columns || "*", orderBy: orderBy.trim() }) => {
    if (!chosen || !targetId) return;
    const op = operation<Preview>("table.view", { scope, targetId, ...chosen, ...filters });
    reading.current = op;
    try {
      const result = normalizePreview(await op.promise);
      startPreviewTransition(() => {
        setPreview({ key: JSON.stringify([targetId, chosen.schema, chosen.table]), result });
        reportTableChange(chosen);
        setDataError("");
      });
    }
    finally { if (reading.current === op) reading.current = undefined; }
  };
  const readData = (chosen = name, filters = { where: [where.trim(), generatedWhere()].filter(Boolean).join(" AND "), select: columns || "*", orderBy: orderBy.trim() }) => task.run(() => readDataRequest(chosen, filters));
  useEffect(() => {
    const chosen = initialTable ? parseTable(initialTable) : undefined;
    if (!visible || !chosen || !knownTable || !targetId || input !== initialTable || preview?.key === key || autoReadKey.current === key) return;
    autoReadKey.current = key;
    void readData(chosen, { where: "", select: "*", orderBy: "" });
    // Parent stores the qualified name after the first read.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [initialTable, input, visible, knownTable, targetId, key]);
  function chooseTable(value: string) {
    const chosen = parseTable(value);
    if (!chosen) return;
    const changing = chosen.schema !== schema || chosen.table !== table;
    if (changing) {
      setWhere("");
      setOrderBy("");
      setColumns("*");
      setSelectedColumns([]);
      setFilterField("");
      setFilterValue("");
      setCarets({ select: 0, where: 0, orderBy: 0 });
    }
    void readData(chosen, changing ? { where: "", select: "*", orderBy: "" } : undefined);
  }
  function captureSqlDataKey() {
    setScriptMenu(false);
    if (!target || !name || !knownTable) {
      setStatus("Chọn một bảng hợp lệ trước khi thêm key vào SQL Data.");
      return;
    }
    const tableName = name.schema === "dbo" ? name.table : `${name.schema}.${name.table}`;
    onCaptureData(target, tableName, where.trim() || "1=1");
    setStatus(`Đã gửi ${tableName}: ${where.trim() || "1=1"} vào SQL Data.`);
  }
  function generateScript(kind: "create" | "drop" | "insert") {
    setScriptMenu(false);
    void task.run(async () => {
      if (!table || !schema || !targetId) return;
      if (kind === "insert") {
        if (!currentPreview?.data.rows.length) return;
        const script = await request<string>("table.insertScript", { scope, snapshotId: currentPreview.snapshotId });
        onText(script, table + "-insert.sql");
        return;
      }
      if (kind === "drop") {
        const qualified = quoteSqlName(schema, table);
        onText(`IF OBJECT_ID(N'${qualified.replaceAll("'", "''")}', N'U') IS NOT NULL\r\nDROP TABLE ${qualified}\r\nGO\r\n`, table + "-drop.sql");
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
  async function commitRows(drafts: [number, (string | null)[]][]) {
    if (readonly || !currentPreview || !currentMetadata || !name || !targetId) return;
    return task.run<{ saved: true } | { saved: false; row: number }>(async () => {
      const visibleColumns = currentPreview.data.columns;
      const writes = drafts.flatMap(([rowIndex, values]) => {
      if (rowIndex >= 0) {
        const original = currentPreview.data.rows[rowIndex];
        if (!original) return [];
        const primary = currentMetadata.filter((column) => column.primaryKey);
        if (!primary.length || primary.some((column) => !visibleColumns.some((name) => name.toLowerCase() === column.name.toLowerCase())))
          throw new Error("Bảng không có đủ khóa chính trong kết quả để cập nhật dòng.");
        const changes = visibleColumns.map((column, index) => ({
          column,
          value: values[index] ?? null,
          old: original[index] ?? null,
        })).filter((entry) => entry.value !== entry.old && currentMetadata.some((meta) =>
          meta.name.toLowerCase() === entry.column.toLowerCase() && meta.writable));
        if (!changes.length) return [];
        const keyEntries = primary.map((column) => {
          const index = visibleColumns.findIndex((name) => name.toLowerCase() === column.name.toLowerCase());
          return { name: column.name, value: original[index] ?? null };
        });
        return [{
          rowIndex,
          mode: "update",
          columns: changes.map((entry) => entry.column),
          values: changes.map((entry) => entry.value),
          keyColumns: keyEntries.map((entry) => entry.name),
          keyValues: keyEntries.map((entry) => entry.value),
        }];
      }
      return [{ rowIndex, mode: "insert", columns: visibleColumns, values, keyColumns: [], keyValues: [] }];
      });
      if (!writes.length) return { saved: true };
      const result = await request<{ conflict?: number | null }>("table.writeBatch", { ...payload, rows: writes });
      if (result.conflict != null) return { saved: false, row: writes[result.conflict].rowIndex };
      setStatus("Đã lưu dữ liệu.");
      try {
        await readDataRequest();
      } catch (error) {
        setDataError((error as Error).message);
        setPreview(undefined);
      }
      return { saved: true };
    });
  }
  async function deleteRows(rowIndexes: number[]) {
    if (readonly || !currentPreview || !currentMetadata || !name || !targetId || !rowIndexes.length) return;
    const visibleColumns = currentPreview.data.columns;
    const primary = currentMetadata.filter((column) => column.primaryKey);
    if (!primary.length || primary.some((column) => !visibleColumns.some((name) => name.toLowerCase() === column.name.toLowerCase()))) {
      setDataError("Bảng không có đủ khóa chính trong kết quả để xóa dòng.");
      return;
    }
    const confirmed = await new Promise<boolean>((resolve) => setDeleteRequest({
      count: rowIndexes.length,
      table: tableIdentity(name),
      resolve,
    }));
    if (!confirmed) return;
    await task.run(async () => {
      const rows = rowIndexes.map((rowIndex) => ({
        rowIndex,
        mode: "delete",
        columns: [],
        values: [],
        keyColumns: primary.map((column) => column.name),
        keyValues: primary.map((column) => {
          const index = visibleColumns.findIndex((name) => name.toLowerCase() === column.name.toLowerCase());
          return currentPreview.data.rows[rowIndex]?.[index] ?? null;
        }),
      }));
      await request("table.writeBatch", { ...payload, rows });
      setStatus(`Đã xóa ${rows.length} dòng.`);
      await readDataRequest();
    });
  }
  function selectColumns(names: string[]) {
    setSelectedColumns(names);
    setColumns(names.length ? names.map((name) => "[" + name.replaceAll("]", "]]" ) + "]").join(", ") : "*");
  }
  function acceptGoTo(value = goToField) {
    const search = value.trim().toLowerCase();
    const found = fieldNames.find((field) => field.toLowerCase() === search)
      ?? fieldNames.find((field) => field.toLowerCase().startsWith(search));
    if (!found) {
      setStatus("Không tìm thấy trường trong kết quả hiện tại.");
      return;
    }
    setGoToRequest({ name: found, sequence: (goToRequest?.sequence ?? 0) + 1 });
    setGoToOpen(false);
  }
  const view = currentPreview?.data;
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
    {targets.length > 0 && <Select floating label="Cơ sở dữ liệu" value={targetId ?? ""} disabled={task.busy || !!drop}
      onContextMenu={(event) => {
        if (!target) return;
        event.preventDefault();
        void writeClipboard(target.database)
          .then(() => setStatus("Đã sao chép tên cơ sở dữ liệu."))
          .catch((error: Error) => setStatus(error.message));
      }}
      options={targets.map((entry) => ({
        value: entry.id,
        label: /_S(?:ys)?$/i.test(entry.database) ? "Sys" : "App",
      }))}
      onChange={(id) => {
        const next = targets.find((entry) => entry.id === id);
        if (next && next.id !== targetId) onTargetChange(next);
      }} />}
    {target && <IconButton label={`Sao chép tên database ${target.database}`} onClick={() => {
      void writeClipboard(target.database)
        .then(() => setStatus(`Đã sao chép ${target.database}.`))
        .catch((error: Error) => setStatus(error.message));
    }}><Copy size={15} /></IconButton>}
    {targetId && <IconButton label="Tải lại danh sách bảng" onClick={() => invalidateTableCatalog(targetId)}>
      <RefreshCw size={15} />
    </IconButton>}
    <div ref={scriptControls} className="relative">
      <Button aria-expanded={scriptMenu} aria-haspopup="menu" disabled={!canRead} onClick={() => setScriptMenu(!scriptMenu)}>Tập lệnh</Button>
      {scriptMenu && <div ref={scriptPopup} className="table-action-menu table-script-menu" role="menu" aria-label="Loại lệnh SQL">
        <Button role="menuitem" variant="ghost" onClick={captureSqlDataKey}>Thêm key vào SQL Data<kbd>Ctrl+D</kbd></Button>
        <Button role="menuitem" variant="ghost" onClick={() => generateScript("create")}>Tạo lệnh tạo bảng</Button>
        <Button role="menuitem" variant="ghost" disabled={!view?.rows.length || previewPending}
          onClick={() => generateScript("insert")}>Tạo lệnh INSERT cho kết quả hiện tại</Button>
        <Button role="menuitem" variant="ghost" onClick={() => generateScript("drop")}>Tạo lệnh xóa bảng</Button>
      </div>}
    </div>
    {target ? <span className="table-toolbar-row-count muted" title={target.server + " / " + target.database}>{view?.rows.length ?? 0} dòng</span>
      : <Button onClick={onConnect}>Kết nối SQL</Button>}
  </div>;
  return (
    <div className="table-inspection flex min-h-0 min-w-0 flex-1 flex-col gap-2 overflow-hidden p-3"
      onKeyDown={event => {
        if (visible && !event.defaultPrevented && (event.ctrlKey || event.metaKey) && !event.altKey && !event.shiftKey && event.key.toLowerCase() === "g") {
          event.preventDefault();
          event.stopPropagation();
          setGoToField("");
          setGoToOpen(true);
          return;
        }
        if (visible && !event.defaultPrevented && (event.ctrlKey || event.metaKey) && !event.altKey && !event.shiftKey && event.key.toLowerCase() === "s") {
          event.preventDefault();
          saveSlot?.querySelector("button")?.click();
          return;
        }
        if (!visible || !(event.ctrlKey || event.metaKey) || event.shiftKey || event.altKey || event.key.toLowerCase() !== "d") return;
        event.preventDefault();
        if (!target || !name || !knownTable) {
          setStatus("Chọn một bảng hợp lệ trước khi nhấn Ctrl+D.");
          return;
        }
        captureSqlDataKey();
      }}>
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
          <div className="table-query-actions">
            <Button variant="primary" className="table-run-button" disabled={!canRead} onClick={() => void readData()} title="F5">
              <Play size={14} />Chạy
            </Button>
            <span ref={setSaveSlot}>{(!currentPreview?.data || !currentMetadata) && <Button disabled>Lưu</Button>}</span>
          </div>
          <span className="table-query-cancel-slot">
            {showQueryCancel && reading.current && <Button onClick={() => void reading.current?.cancel()}>Dừng</Button>}
          </span>
        </div>
        <div className="table-query-clauses">
          <SuggestedInput label="WHERE" placeholder="Điều kiện WHERE (không bắt buộc)" value={where} multiline
            options={clauseSuggestions(where, Math.min(carets.where, where.length), fieldNames, "where")}
            onCaret={(position) => setCarets((old) => old.where === position ? old : { ...old, where: position })}
            onChange={setWhere} disabled={false} className="table-query-where" />
          <SuggestedInput label="ORDER BY" placeholder="Trường sắp xếp, ví dụ: ngay_ct DESC" value={orderBy}
            options={clauseSuggestions(orderBy, Math.min(carets.orderBy, orderBy.length), fieldNames, "orderBy")}
            onCaret={(position) => setCarets((old) => old.orderBy === position ? old : { ...old, orderBy: position })}
            onChange={setOrderBy} disabled={false} className="table-query-order"
            onSubmit={() => { if (canRead) void readData(); }} />
        </div>
      </div>
      <TableToolbarPortal slotId={toolbarSlotId}>{toolbarControls}</TableToolbarPortal>
      {targetId && !names.length && <p className="muted" role="status">
        {tables ? "Không tìm thấy bảng phù hợp." : catalogError ? "Danh sách bảng chưa có dữ liệu." : "Đang tải danh sách bảng…"}
      </p>}
      {visible && <WorkbenchNotice error={task.error || dataError || catalogError} status={status} onClose={() => {
        task.clearError();
        setDataError("");
        setCatalogError("");
        setStatus("");
      }} />}
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
          {currentPreview?.data && currentMetadata ? <TableDataGrid
            saveSlot={saveSlot}
            focusColumn={goToRequest}
            table={currentPreview.data} metadata={currentMetadata} readonly={readonly} busy={task.busy || previewPending} onCommit={commitRows}
            onDelete={deleteRows}
            emptyMessage={filterField && (filterOperator === "IS NULL" || filterOperator === "IS NOT NULL" || filterValue.trim())
              ? "Không tìm thấy dữ liệu phù hợp."
              : undefined} />
            : <div className="empty">{task.busy ? "Đang đọc bảng…" : "Chọn bảng để đọc tối đa 1.000 dòng dữ liệu."}</div>}
        </section>
      </div>
      {currentMetadata && <div className="table-filter-row">
        <Select floating label="Trường lọc" value={filterField} options={filterOptions} onChange={setFilterField} disabled={!fieldNames.length} />
        <Select floating label="Toán tử" value={filterOperator} options={operatorOptions} onChange={setFilterOperator} disabled={!filterField} />
        <input aria-label="Giá trị lọc" placeholder="Giá trị" value={filterValue} onChange={(event) => setFilterValue(event.target.value)} disabled={!filterField || filterOperator === "IS NULL" || filterOperator === "IS NOT NULL"} />
        <Button variant="secondary" disabled={!canRead || !filterField} onClick={() => void readData()}>Lọc</Button>
      </div>}
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
          void readData();
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
      {goToOpen && <Modal title="Đi đến trường" onClose={() => setGoToOpen(false)} className="table-goto-modal">
        <div className="tool-body">
          <SuggestedInput label="Tên trường" placeholder="Nhập hoặc chọn trường…" value={goToField} autoFocus disabled={!fieldNames.length}
            options={fieldNames.filter((field) => field.toLowerCase().includes(goToField.trim().toLowerCase()))
              .slice(0, 40).map((field) => ({ label: field, value: field }))}
            onChange={setGoToField} onChoose={(value) => { setGoToField(value); acceptGoTo(value); }} onSubmit={() => acceptGoTo()} />
          <div className="flex justify-end gap-2">
            <Button onClick={() => setGoToOpen(false)}>Hủy</Button>
            <Button variant="primary" disabled={!goToField.trim()} onClick={() => acceptGoTo()}>Nhận</Button>
          </div>
        </div>
      </Modal>}
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
                await readDataRequest();
                setStatus("Đã xóa cột.");
              })}>Xóa cột</Button>
            </div>
          </div>
        </Modal>
      )}
      {deleteRequest && <Modal title="Xóa dữ liệu" className="table-delete-modal" onClose={() => {
        const pending = deleteRequest;
        setDeleteRequest(undefined);
        pending.resolve(false);
      }}>
        <div className="tool-body">
          <div className="table-delete-message">
            <CircleAlert size={20} aria-hidden />
            <div><strong>Xóa {deleteRequest.count} dòng khỏi {deleteRequest.table}?</strong>
              <p>Dữ liệu đã xóa không thể khôi phục bằng thao tác hoàn tác.</p></div>
          </div>
          <div className="flex flex-wrap justify-end gap-2">
            <Button onClick={() => {
              const pending = deleteRequest;
              setDeleteRequest(undefined);
              pending.resolve(false);
            }}>Hủy</Button>
            <Button className="table-drop-action" onClick={() => {
              const pending = deleteRequest;
              setDeleteRequest(undefined);
              pending.resolve(true);
            }}>Xóa dữ liệu</Button>
          </div>
        </div>
      </Modal>}
    </div>
  );
}

type TableToolErrorBoundaryProps = { children: ReactNode; resetKey?: string };
type TableToolErrorBoundaryState = { error?: Error };

export class TableToolErrorBoundary extends Component<TableToolErrorBoundaryProps, TableToolErrorBoundaryState> {
  state: TableToolErrorBoundaryState = {};

  static getDerivedStateFromError(error: Error): TableToolErrorBoundaryState {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error("TableTool render failed", error, info);
  }

  componentDidUpdate(previous: TableToolErrorBoundaryProps) {
    if (this.state.error && previous.resetKey !== this.props.resetKey) this.setState({ error: undefined });
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
