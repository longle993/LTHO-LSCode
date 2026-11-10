import { useEffect, useId, useRef, useState } from "react";
import { Play } from "lucide-react";
import { Button, DataGrid, Modal } from "../ui";
import { operation, quoteSqlName, request, writeClipboard } from "../bridge";
import type { Table, Target } from "../bridge";
import { useTask } from "./useTask";

type TableName = { schema: string; table: string };
type Preview = { data: Table; snapshotId: string };
type ColumnMenu = { x: number; y: number; columns: string[] };
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
const unquote = (name: string) => name.startsWith("[") && name.endsWith("]")
  ? name.slice(1, -1).replaceAll("]]", "]") : name;
function columnSuggestions(input: string, columns: string[]) {
  const tail = input.match(/(?:\[[^\]]*|[\p{L}\p{N}_$#@]*)$/u)?.[0] ?? "";
  const prefix = input.slice(0, input.length - tail.length);
  const search = tail.replace(/^\[/, "").toLowerCase();
  return columns.filter((name) => name.toLowerCase().includes(search))
    .slice(0, 24).map((name) => prefix + quote(name));
}

function SuggestedInput({ label, placeholder, value, options, disabled, onChange, onSubmit, className }: {
  label: string; placeholder?: string; value: string; options: string[]; disabled: boolean;
  onChange: (value: string) => void; onSubmit?: () => void; className?: string;
}) {
  const id = useId();
  const [open, setOpen] = useState(false), [active, setActive] = useState(-1);
  const visible = open && options.length > 0 && !disabled;
  const current = active < options.length ? active : -1;
  function choose(option: string) {
    onChange(option);
    setOpen(false);
    setActive(-1);
  }
  return (
    <div className={`relative ${className ?? ""}`}>
      <input role="combobox" aria-label={label} aria-autocomplete="list"
        aria-expanded={visible} aria-controls={visible ? id : undefined}
        aria-activedescendant={visible && current >= 0 ? id + "-" + current : undefined}
        placeholder={placeholder ?? label} value={value} disabled={disabled}
        onFocus={() => setOpen(true)} onBlur={() => setOpen(false)}
        onChange={(event) => { setOpen(true); setActive(-1); onChange(event.target.value); }}
        onKeyDown={(event) => {
          if (event.key === "Escape") { setOpen(false); setActive(-1); }
          if ((event.key === "ArrowDown" || event.key === "ArrowUp") && options.length) {
            event.preventDefault();
            setOpen(true);
            setActive((event.key === "ArrowDown" ? current + 1 : current <= 0 ? options.length - 1 : current - 1) % options.length);
          }
          if (event.key === "Enter" && visible && current >= 0) {
            event.preventDefault();
            choose(options[current]);
          } else if (event.key === "Enter") onSubmit?.();
        }} />
      {visible && <ul id={id} role="listbox" className="table-suggestions">
        {options.map((option, index) => (
          <li key={option} id={id + "-" + index} role="option" aria-selected={index === current}
            onPointerDown={(event) => event.preventDefault()} onClick={() => choose(option)}>
            {option}
          </li>
        ))}
      </ul>}
    </div>
  );
}

export function TableTool({ target, onText, onConnect }: {
  target?: Target;
  onText: (text: string, name: string) => void;
  onConnect: () => void;
}) {
  const [input, setInput] = useState(""), [where, setWhere] = useState(""),
    [orderBy, setOrderBy] = useState(""), [columns, setColumns] = useState(""),
    [mode, setMode] = useState<"data" | "structure">("data"),
    [names, setNames] = useState<string[]>([]),
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
  const currentStructure = structure?.key === key ? structure.data : undefined;
  const currentPreview = preview?.key === key ? preview.result : undefined;
  const fieldNames = currentStructure?.rows.flatMap((row) => row[0] ? [row[0]] : []) ?? [];
  const payload = { targetId, schema, table };
  const canRead = !!targetId && !!name && !task.busy;
  useEffect(() => {
    if (!targetId) { setNames([]); return; }
    let current = true;
    let pending: ReturnType<typeof operation<Table>> | undefined;
    const timer = window.setTimeout(() => {
      pending = operation<Table>("table.names", { targetId, query: input.replace(/[\[\]]/g, "").trim() });
      void pending.promise.then((result) => {
        if (current) setNames(result.rows.flatMap(([s, n]) => s && n
          ? [s === "dbo" && /^[\p{L}\p{N}_$#@]+$/u.test(n) ? n : quoteSqlName(s, n)] : []));
      }).catch(() => { if (current) setNames([]); });
    }, 220);
    return () => {
      current = false;
      window.clearTimeout(timer);
      void pending?.cancel().catch(() => {});
    };
  }, [targetId, input]);
  useEffect(() => {
    setSelectedRows([]);
    setMenu(undefined);
    setDrop(undefined);
    setStatus("");
    if (!targetId || !schema || !table) return;
    let current = true;
    let pending: ReturnType<typeof operation<Table>> | undefined;
    const timer = window.setTimeout(() => {
      pending = operation<Table>("table.structure", { targetId, schema, table });
      void pending.promise.then((data) => {
        if (current) setStructure({ key, data });
      }).catch(() => {});
    }, 220);
    return () => {
      current = false;
      window.clearTimeout(timer);
      void pending?.cancel().catch(() => {});
    };
  }, [key, targetId, schema, table]);
  useEffect(() => () => { void reading.current?.cancel().catch(() => {}); }, []);
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

  const readStructure = async () => {
    const data = await request<Table>("table.structure", payload);
    setStructure({ key, data });
    setSelectedRows([]);
  };
  const readData = () => task.run(async () => {
    if (!canRead) return;
    const op = operation<Preview>("table.view", {
      ...payload, where, orderBy,
      columns: (columns.match(/(?:\[(?:[^\]]|\]\])*\]|[^,\[])+/gu) ?? [])
        .map((column) => unquote(column.trim())).filter(Boolean),
    });
    reading.current = op;
    try { setPreview({ key, result: await op.promise }); }
    finally { if (reading.current === op) reading.current = undefined; }
  });
  function generateScript(kind: "create" | "insert") {
    setScriptMenu(false);
    void task.run(async () => {
      if (!table || !schema || !targetId) return;
      if (kind === "insert") {
        if (currentPreview?.data.rows.length) {
          const script = await request<string>("table.insertScript", { snapshotId: currentPreview.snapshotId });
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
          onChange={setInput} disabled={task.busy} className="table-name-input"
          onSubmit={() => { if (canRead) void (mode === "data" ? readData() : task.run(readStructure)); }} />
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
      <div className="flex shrink-0 flex-wrap items-center gap-2">
        {mode === "data" && ([
          ["columns", "Cột SELECT", columns, setColumns],
          ["where", "WHERE", where, setWhere],
          ["order", "ORDER BY", orderBy, setOrderBy],
        ] as const).map(([field, label, value, setValue]) => (
          <SuggestedInput key={field} label={label} value={value} options={columnSuggestions(value, fieldNames)}
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
        <DataGrid key={mode + key} table={view}
          selectedRows={mode === "structure" ? selectedRows : undefined}
          onSelectionChange={mode === "structure" ? setSelectedRows : undefined}
          onRowContextMenu={mode === "structure" && !task.busy ? (row, event) => {
            event.preventDefault();
            const rows = selectedRows.includes(row) ? selectedRows : [row];
            setSelectedRows(rows);
            setMenu({
              x: Math.max(8, Math.min(event.clientX, innerWidth - 224)),
              y: Math.max(8, Math.min(event.clientY, innerHeight - 96)),
              columns: rows.flatMap((index) => view.rows[index]?.[0] ? [view.rows[index][0]!] : []),
            });
          } : undefined} />
      ) : <div className="empty">{task.busy ? "Đang đọc bảng…" : "Chọn bảng để xem cấu trúc hoặc đọc tối đa 1.000 dòng dữ liệu."}</div>}
      {menu && (
        <div ref={contextMenu} className="table-action-menu fixed z-20" style={{ left: menu.x, top: menu.y }} aria-label="Thao tác cột">
          <Button variant="ghost" onClick={() => {
            setMenu(undefined);
            void task.run(async () => {
              const script = await request<string>("table.addScript", { ...payload, columns: menu.columns });
              await writeClipboard(script);
              setStatus("Đã sao chép script ALTER TABLE … ADD.");
            });
          }}>Gen ALTER / ADD COLUMN</Button>
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
