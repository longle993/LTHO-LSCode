import { useEffect, useId, useLayoutEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Button } from "../ui";
import { KeyRound } from "lucide-react";
import { writeClipboard, type Table } from "../bridge";

export type TableColumn = {
  name: string;
  sqlType: string;
  key: string;
  primaryKey: boolean;
  nullable: boolean;
  identity: boolean;
  computed: boolean;
  generated: number;
  writable: boolean;
};

const rowHeight = 32;

function isUniqueKey(column?: TableColumn) {
  return !!column && (column.primaryKey || /(?:^|, )(PK|UQ) ·/.test(column.key));
}

function displayCell(value: string, column?: TableColumn) {
  const type = column?.sqlType.trim().toLowerCase();
  if (type !== "datetime" && type !== "smalldatetime") return value;
  const parts = /^(\d{4})-(\d{2})-(\d{2})[T ](\d{2}:\d{2})/.exec(value);
  if (!parts) return value;
  const [, year, month, day, time] = parts;
  const date = `${day}/${month}/${year}`;
  return type === "datetime" ? `${date} ${time}` : date;
}

export function TableDataGrid({
  table,
  metadata,
  readonly,
  busy,
  onCommit,
  onDelete,
  saveSlot,
  focusColumn,
  rowOffset,
  onPendingChange,
  emptyMessage = "Chưa có dữ liệu. Nhập ở dòng * rồi bấm Lưu để thêm.",
}: {
  table: Table;
  metadata: TableColumn[];
  readonly: boolean;
  busy: boolean;
  onCommit: (rows: [number, (string | null)[]][]) => Promise<{ saved: true } | { saved: false; row: number } | undefined>;
  onDelete: (rows: number[]) => Promise<void>;
  saveSlot: HTMLSpanElement | null;
  focusColumn?: { name: string; sequence: number };
  rowOffset: number;
  onPendingChange: (dirty: boolean) => void;
  emptyMessage?: string;
}) {
  const [drafts, setDrafts] = useState<Record<string, (string | null)[]>>({});
  const [selection, setSelection] = useState<{
    row: number;
    column: number;
    endRow: number;
    endColumn: number;
    kind: "cell" | "row" | "column";
  }>();
  const [editing, setEditing] = useState<{ row: number; column: number }>();
  const [copyError, setCopyError] = useState("");
  const grid = useRef<HTMLDivElement>(null);
  const body = useRef<HTMLTableSectionElement>(null);
  const [viewport, setViewport] = useState({ top: 0, height: 320 });
  const gridId = useId();
  const saving = useRef(false);
  const [conflict, setConflict] = useState<number>();
  const [editStart, setEditStart] = useState<string | null>();
  const dragging = useRef(false);
  const [showLoading, setShowLoading] = useState(false);
  const measure = () => {
    const element = grid.current;
    const rows = body.current;
    const header = rows?.previousElementSibling;
    if (!element || !rows || !header) return;
    const top = Math.max(0, element.getBoundingClientRect().top + header.clientHeight - rows.getBoundingClientRect().top);
    const height = element.clientHeight;
    setViewport(previous => previous.top === top && previous.height === height ? previous : { top, height });
  };
  useLayoutEffect(() => {
    setDrafts({});
    setSelection(undefined);
    setEditing(undefined);
    setConflict(undefined);
    if (grid.current) grid.current.scrollTop = 0;
    measure();
  }, [table]);
  useLayoutEffect(() => {
    if (!grid.current) return;
    const observer = new ResizeObserver(measure);
    observer.observe(grid.current);
    return () => observer.disconnect();
  }, []);
  useEffect(() => {
    if (!busy) {
      setShowLoading(false);
      return;
    }
    const timer = window.setTimeout(() => setShowLoading(true), 250);
    return () => window.clearTimeout(timer);
  }, [busy]);
  const safeColumns = table.columns;
  const safeRows = table.rows;
  const safeMetadata = metadata;
  const columns = useMemo(() => {
    const byName = new Map(metadata.map(column => [column.name.toLowerCase(), column]));
    return safeColumns.map(name => byName.get(name.toLowerCase()));
  }, [metadata, safeColumns]);
  const widths = useMemo(() => safeColumns.map((name, index) => {
    const length = safeRows.slice(0, 8).reduce((max, row) => Math.max(max, row[index]?.length ?? 0), name.length + 4);
    return Math.min(320, Math.max(116, length * 7 + 24));
  }), [safeColumns, safeRows]);
  const first = Math.min(Math.max(0, safeRows.length - 1), Math.max(0, Math.floor(viewport.top / rowHeight) - 5));
  const last = Math.min(safeRows.length, Math.ceil((viewport.top + viewport.height) / rowHeight) + 5);
  const keyboardScroll = useRef(false);
  useLayoutEffect(() => {
    if (!keyboardScroll.current || !selection || selection.kind !== "cell") return;
    const element = grid.current;
    const cell = element?.querySelector<HTMLElement>(`[data-table-cell="${selection.endRow}:${selection.endColumn}"]`);
    if (!cell && element && body.current) {
      const bodyTop = body.current.getBoundingClientRect().top - element.getBoundingClientRect().top;
      element.scrollTop += bodyTop - rowHeight + selection.endRow * rowHeight;
      measure();
      return;
    }
    keyboardScroll.current = false;
    cell?.scrollIntoView({ block: "nearest", inline: "nearest" });
  }, [selection, first, last]);
  useEffect(() => {
    if (!focusColumn || !safeRows.length) return;
    const column = safeColumns.findIndex((name) => name.toLowerCase() === focusColumn.name.toLowerCase());
    if (column < 0) return;
    keyboardScroll.current = true;
    setSelection((current) => {
      const row = Math.max(0, Math.min(safeRows.length - 1,
        current?.kind === "column" ? 0 : current?.endRow ?? 0));
      return { row, column, endRow: row, endColumn: column, kind: "cell" };
    });
    requestAnimationFrame(() => grid.current?.focus({ preventScroll: true }));
  }, [focusColumn?.sequence]);
  const keyVisible = safeMetadata.filter((column) => column.primaryKey)
    .every((column) => safeColumns.some((name) => name.toLowerCase() === column.name.toLowerCase()));
  const canUpdate = keyVisible && safeMetadata.some((column) => column.primaryKey);
  const valueAt = (row: number, column: number) => {
    const draft = drafts[String(row)];
    if (draft) return draft[column] ?? "";
    if (row >= 0 && row < safeRows.length) return safeRows[row]?.[column] ?? "";
    return "";
  };
  function edit(row: number, column: number, value: string | null) {
    setDrafts((old) => {
      const next = { ...old };
      const current = [...(old[String(row)] ?? (row >= 0 ? safeRows[row] : safeColumns.map(() => "")))];
      current[column] = value;
      next[String(row)] = current;
      return next;
    });
  }
  const pending = Object.entries(drafts).filter(([index, values]) =>
    values.some((value, column) => value !== (Number(index) < 0 ? "" : safeRows[Number(index)]?.[column])));
  const dirty = pending.length > 0;
  useLayoutEffect(() => {
    onPendingChange(dirty);
    return () => onPendingChange(false);
  }, [dirty, onPendingChange]);
  async function save() {
    if (readonly || busy || saving.current || !pending.length) return;
    saving.current = true;
    setEditing(undefined);
    setConflict(undefined);
    try {
      const result = await onCommit(pending.map(([index, values]) => [Number(index), values]));
      if (result?.saved) setDrafts({});
      else if (result?.saved === false) {
        setConflict(result.row);
        setDrafts(old => {
          const values = old[result.row];
          if (!values) return old;
          const original = safeRows[result.row];
          return { ...old, [result.row]: values.map((value, index) =>
            isUniqueKey(columns[index])
              ? original ? original[index] : "" : value) };
        });
      }
    } finally {
      saving.current = false;
    }
  }
  function beginEdit(row: number, column: number) {
    const metadata = columns[column];
    if (readonly || busy || !canUpdate || !metadata?.writable) return;
    setEditStart((drafts[row] ?? safeRows[row])[column]);
    setEditing({ row, column });
    requestAnimationFrame(() => grid.current?.querySelector<HTMLInputElement>(`[data-edit-cell="${row}:${column}"]`)?.focus());
  }
  const range = (start: number, end: number) =>
    `:nth-child(n+${Math.min(start, end)}):nth-child(-n+${Math.max(start, end)})`;
  const rowStart = selection ? Math.max(first, Math.min(selection.row, selection.endRow)) : first;
  const rowEnd = selection ? Math.min(last - 1, Math.max(selection.row, selection.endRow)) : last - 1;
  const rowIndex = (index: number) => index - first + (first ? 2 : 1);
  const selectionRule = selection && (selection.kind === "column" || rowStart <= rowEnd) && `#${CSS.escape(gridId)} tbody > .table-data-row${
    selection.kind === "column" ? "" : range(rowIndex(rowStart), rowIndex(rowEnd))
  } > td${
    selection.kind === "row" ? "" : range(selection.column + 2, selection.endColumn + 2)
  }:not(:has(input)) { background: color-mix(in srgb, var(--primary) 13%, var(--surface)); outline: 1px solid var(--primary); outline-offset: -1px; }`;
  async function copySelection() {
    if (!selection) return;
    const rowStart = selection.kind === "column" ? 0 : Math.min(selection.row, selection.endRow);
    const rowEnd = selection.kind === "column" ? safeRows.length - 1 : Math.max(selection.row, selection.endRow);
    const columnStart = selection.kind === "row" ? 0 : Math.min(selection.column, selection.endColumn);
    const columnEnd = selection.kind === "row" ? safeColumns.length - 1 : Math.max(selection.column, selection.endColumn);
    const quote = (value: string | null) => /[\t\r\n"]/.test(value ?? "")
      ? `"${(value ?? "").replaceAll('"', '""')}"` : value ?? "";
    try {
      await writeClipboard(safeRows.slice(rowStart, rowEnd + 1)
        .map((row, offset) => (drafts[rowStart + offset] ?? row).slice(columnStart, columnEnd + 1).map(quote).join("\t")).join("\r\n"));
      setCopyError("");
    } catch (error) { setCopyError((error as Error).message); }
  }
  const gridBody = useMemo(() =>
        <tbody ref={body}>
          {first > 0 && <tr className="table-grid-spacer" aria-hidden><td colSpan={safeColumns.length + 1} style={{ height: first * rowHeight }} /></tr>}
          {safeRows.slice(first, last).map((row, index) => {
            const rowIndex = first + index;
            return <tr key={rowIndex} className="table-data-row">
              <td className="row-number" title="Chọn dòng · Ctrl+C sao chép"
                onPointerDown={(event) => {
                  if (event.button !== 0) return;
                  event.preventDefault();
                  dragging.current = true;
                  grid.current?.focus({ preventScroll: true });
                  setSelection(old => event.shiftKey && old?.kind === "row"
                    ? { ...old, endRow: rowIndex }
                    : { row: rowIndex, column: 0, endRow: rowIndex, endColumn: 0, kind: "row" });
                }}
                onPointerEnter={event => dragging.current && event.buttons === 1 && setSelection(old =>
                  old?.kind === "row" && old.endRow !== rowIndex ? { ...old, endRow: rowIndex } : old)}>{rowOffset + rowIndex + 1}</td>
              {row.map((original, columnIndex) => {
                const value = drafts[rowIndex] ? drafts[rowIndex][columnIndex] : original;
                const column = columns[columnIndex];
                const isEditing = editing?.row === rowIndex && editing.column === columnIndex;
                return <td key={columnIndex} title={value == null ? "NULL" : value.length > 480 ? value.slice(0, 480) + "…" : value} data-table-cell={`${rowIndex}:${columnIndex}`}
                  aria-invalid={conflict === rowIndex && isUniqueKey(column) || undefined}
                  className={column?.key ? "table-key-column" : undefined}
                  onPointerDown={event => {
                    if (event.button !== 0 || isEditing) return;
                    event.preventDefault();
                    dragging.current = true;
                    grid.current?.focus({ preventScroll: true });
                    setSelection(old => event.shiftKey && old?.kind === "cell"
                      ? { ...old, endRow: rowIndex, endColumn: columnIndex }
                      : { row: rowIndex, column: columnIndex, endRow: rowIndex, endColumn: columnIndex, kind: "cell" });
                  }}
                  onPointerEnter={event => dragging.current && event.buttons === 1 && setSelection(old =>
                    old && (old.endRow !== rowIndex || (old.kind === "cell" && old.endColumn !== columnIndex))
                      ? { ...old, endRow: rowIndex, endColumn: columnIndex } : old)}
                  onDoubleClick={() => beginEdit(rowIndex, columnIndex)}>
                  {isEditing ? <input data-edit-cell={`${rowIndex}:${columnIndex}`}
                    aria-label={`${safeColumns[columnIndex]} dòng ${rowOffset + rowIndex + 1}`}
                    value={value ?? ""} disabled={busy || readonly}
                    onChange={event => edit(rowIndex, columnIndex, event.target.value)}
                    onBlur={() => setEditing(undefined)}
                    onKeyDown={event => {
                      if (event.key !== "Enter" && event.key !== "Escape") return;
                      event.preventDefault();
                      if (event.key === "Escape") edit(rowIndex, columnIndex, editStart ?? null);
                      setEditing(undefined);
                      grid.current?.focus({ preventScroll: true });
                    }} /> : value == null ? <span className="muted">NULL</span> : displayCell(value.length > 480 ? value.slice(0, 480) + "…" : value, column)}
                </td>;
              })}
            </tr>;
          })}
          {last < safeRows.length && <tr className="table-grid-spacer" aria-hidden><td colSpan={safeColumns.length + 1} style={{ height: (safeRows.length - last) * rowHeight }} /></tr>}
        </tbody>, [table, metadata, readonly, busy, drafts, editing, editStart, conflict, first, last, rowOffset]);
  return (
    <div ref={grid} id={gridId} className="table-data-grid data-grid data-grid-range" tabIndex={0}
      aria-label="Dữ liệu bảng · Chọn ô và dùng phím mũi tên để di chuyển, nhấp đúp để sửa, Ctrl+C sao chép, F8 xóa dòng"
      aria-busy={busy} data-loading={showLoading ? "true" : undefined}
      onScroll={measure}
      onPointerUp={() => { dragging.current = false; }}
      onPointerCancel={() => { dragging.current = false; }}
      onKeyDown={(event) => {
        if ((event.ctrlKey || event.metaKey) && !event.altKey && !event.shiftKey && event.key.toLowerCase() === "s") {
          event.preventDefault();
          void save();
          return;
        }
        if (event.target instanceof HTMLInputElement) return;
        if (event.key === "F8" && selection && safeRows.length) {
          event.preventDefault();
          const first = selection.kind === "column" ? 0 : Math.min(selection.row, selection.endRow);
          const last = selection.kind === "column" ? safeRows.length - 1 : Math.max(selection.row, selection.endRow);
          void onDelete(Array.from({ length: last - first + 1 }, (_, index) => first + index));
        } else if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "c" && selection) {
          event.preventDefault();
          void copySelection();
        } else if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "a" && safeRows.length && safeColumns.length) {
          event.preventDefault();
          setSelection({ row: 0, column: 0, endRow: safeRows.length - 1, endColumn: safeColumns.length - 1, kind: "cell" });
        } else if (event.key === "F2" && selection?.kind === "cell") {
          event.preventDefault();
          beginEdit(selection.endRow, selection.endColumn);
        } else if (selection && (event.key === "Home" || event.key === "End") && safeColumns.length && safeRows.length) {
          event.preventDefault();
          keyboardScroll.current = true;
          const row = Math.max(0, Math.min(safeRows.length - 1, selection.kind === "column" ? 0 : selection.endRow));
          const column = event.key === "Home" ? 0 : safeColumns.length - 1;
          setSelection({ row, column, endRow: row, endColumn: column, kind: "cell" });
        } else if (selection?.kind === "cell" && ["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Tab"].includes(event.key)) {
          event.preventDefault();
          keyboardScroll.current = true;
          const rowDelta = event.key === "ArrowUp" ? -1 : event.key === "ArrowDown" ? 1 : 0;
          const columnDelta = event.key === "ArrowLeft" || (event.key === "Tab" && event.shiftKey) ? -1
            : event.key === "ArrowRight" || event.key === "Tab" ? 1 : 0;
          let row = selection.endRow + rowDelta;
          let column = selection.endColumn + columnDelta;
          if (event.key === "Tab" && column >= safeColumns.length) { column = 0; row += 1; }
          if (event.key === "Tab" && column < 0) { column = Math.max(0, safeColumns.length - 1); row -= 1; }
          row = Math.max(0, Math.min(safeRows.length - 1, row));
          column = Math.max(0, Math.min(safeColumns.length - 1, column));
          setSelection(old => event.shiftKey && event.key !== "Tab" && old
            ? { ...old, endRow: row, endColumn: column }
            : { row, column, endRow: row, endColumn: column, kind: "cell" });
        }
      }}>
      <style>{selectionRule}</style>
      {saveSlot && createPortal(
        <Button disabled={readonly || busy || !pending.length} onClick={() => void save()} title="Ctrl+S">Lưu</Button>,
        saveSlot,
      )}
      {conflict !== undefined && <p role="alert" className="table-grid-warning error-text">Trùng khóa tại {conflict < 0 ? "dòng mới" : `dòng ${conflict + 1}`}. Đã khôi phục ô khóa.</p>}
      {showLoading && <div className="table-data-loading" role="status">Đang lọc dữ liệu…</div>}
      {copyError && <span className="table-grid-warning" role="alert">{copyError}</span>}
      {!readonly && !canUpdate && safeRows.length > 0 && (
        <p className="table-grid-warning" role="status">
          Bảng không có đủ khóa chính trong kết quả hiện tại; chỉ có thể thêm dòng mới.
        </p>
      )}
      <table style={{ width: 52 + widths.reduce((sum, width) => sum + width, 0) }}>
        <colgroup><col style={{ width: 52 }} />{widths.map((width, index) =>
          <col key={index} style={index === widths.length - 1 ? undefined : { width }} />)}</colgroup>
        <thead>
          <tr>
            <th className="row-number"
              title="Chọn toàn bộ · Ctrl+C sao chép"
              onClick={() => {
                grid.current?.focus({ preventScroll: true });
                if (safeRows.length && safeColumns.length) setSelection({
                row: 0, column: 0, endRow: safeRows.length - 1, endColumn: safeColumns.length - 1, kind: "cell",
                });
              }}>#</th>
            {safeColumns.map((name, index) => {
              const column = columns[index];
              return <th key={name + index} title={`${column?.sqlType ?? name} · Chọn cột, Ctrl+C sao chép`}
                className={column?.key ? "table-key-column" : undefined}
                onClick={(event) => {
                  grid.current?.focus({ preventScroll: true });
                  setSelection(old => event.shiftKey && old?.kind === "column"
                  ? { ...old, endColumn: index }
                  : { row: 0, column: index, endRow: Math.max(0, safeRows.length - 1), endColumn: index, kind: "column" });
                }}>
                {column?.key && <KeyRound size={13} aria-label={column.primaryKey ? "Khóa chính" : "Khóa"} />}
                {name}
              </th>;
            })}
          </tr>
        </thead>
        {gridBody}
        <tbody>
          <tr className="table-new-row">
            <td className="row-number" title="Dòng mới">*</td>
            {safeColumns.map((name, columnIndex) => {
              const column = columns[columnIndex];
              const editable = !readonly && !!column?.writable;
              return <td key={name + columnIndex} aria-invalid={conflict === -1 && isUniqueKey(column) || undefined} className={column?.key ? "table-key-column" : undefined}>
                {editable ? <input
                  aria-label={`${name} dòng mới`}
                  placeholder={column?.nullable ? "NULL" : "Nhập giá trị"}
                  value={String(valueAt(-1, columnIndex))}
                  disabled={busy}
                  onChange={(event) => edit(-1, columnIndex, event.target.value)}
                  onKeyDown={(event) => {
                    if (event.key === "Enter") {
                      event.preventDefault();
                      grid.current?.focus({ preventScroll: true });
                    }
                  }}
                /> : <span className="muted">{column?.identity || column?.computed ? "Tự sinh" : "—"}</span>}
              </td>;
            })}
          </tr>
        </tbody>
      </table>
      {!safeRows.length && <div className="empty">{emptyMessage}</div>}
    </div>
  );
}
