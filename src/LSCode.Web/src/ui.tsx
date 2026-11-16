import { writeClipboard } from "./bridge";
import { useEffect, useLayoutEffect, useRef, useState, useId } from "react";
import type { ButtonHTMLAttributes, ReactNode } from "react";
import { X, ChevronDown, Check } from "lucide-react";
import type { Table } from "./bridge";
export function Button({
  children,
  variant = "outline",
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: "outline" | "primary" | "secondary" | "ghost";
}) {
  return (
    <button {...props} className={`button ${variant} ${props.className ?? ""}`}>
      {children}
    </button>
  );
}
export function IconButton({
  label,
  children,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { label: string }) {
  return (
    <Button
      {...props}
      variant="ghost"
      className="icon-button"
      title={label}
      aria-label={label}
    >
      {children}
    </Button>
  );
}
export function Select({
  value,
  options,
  onChange,
  label,
  disabled = false,
  searchOptions,
  floating = false,
}: {
  value: string;
  options: { value: string; label: string }[];
  onChange: (v: string) => void;
  label: string;
  disabled?: boolean;
  searchOptions?: string;
  floating?: boolean;
}) {
  const [open, setOpen] = useState(false);
  const [filter, setFilter] = useState("");
  const visible = options.filter(option => !searchOptions || option.label.toLowerCase().includes(filter.trim().toLowerCase()));
  const listId = useId();
  const root = useRef<HTMLDivElement>(null);
  const popup = useRef<HTMLDivElement>(null);
  const trigger = useRef<HTMLButtonElement>(null);
  useLayoutEffect(() => {
    const menu = popup.current;
    const button = trigger.current;
    if (!open || !floating || !menu || !button) return;
    if (typeof menu.showPopover === "function") {
      menu.setAttribute("popover", "manual");
      menu.showPopover();
    }
    const place = () => {
      const rect = button.getBoundingClientRect();
      const below = window.innerHeight - rect.bottom - 12;
      const above = rect.top - 12;
      const upward = below < 240 && above > below;
      const width = Math.min(Math.max(rect.width, 248), window.innerWidth - 24);
      Object.assign(menu.style, {
        width: width + "px",
        left: Math.max(12, Math.min(rect.left, window.innerWidth - width - 12)) + "px",
        maxHeight: Math.min(280, Math.max(0, upward ? above : below)) + "px",
        top: upward ? "auto" : rect.bottom + 4 + "px",
        bottom: upward ? window.innerHeight - rect.top + 4 + "px" : "auto",
      });
    };
    place();
    menu.querySelector("input")?.focus({ preventScroll: true });
    window.addEventListener("resize", place);
    document.addEventListener("scroll", place, true);
    return () => {
      window.removeEventListener("resize", place);
      document.removeEventListener("scroll", place, true);
      if (menu.hasAttribute("popover") && menu.matches(":popover-open")) menu.hidePopover();
    };
  }, [open, floating]);
  useEffect(() => {
    const outside = (e: PointerEvent) => {
      if (!root.current?.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("pointerdown", outside);
    return () => document.removeEventListener("pointerdown", outside);
  }, []);
  return (
    <div
      className="select"
      ref={root}
      onKeyDown={(e) => {
        if (e.key === "Escape" && open) {
          e.preventDefault();
          e.stopPropagation();
          setOpen(false);
          root.current?.querySelector("button")?.focus();
        }
        if (
          e.key === "ArrowDown" ||
          e.key === "ArrowUp" ||
          e.key === "Home" ||
          e.key === "End"
        ) {
          if (e.target instanceof HTMLInputElement && (e.key === "Home" || e.key === "End")) return;
          e.preventDefault();
          if (!open) {
            setFilter("");
            setOpen(true);
            requestAnimationFrame(() =>
              root.current
                ?.querySelector<HTMLElement>(searchOptions ? "input" : "[role=option]")
                ?.focus({ preventScroll: floating }),
            );
            return;
          }
          const items = Array.from(
            root.current?.querySelectorAll<HTMLButtonElement>(
              "[role=option]",
            ) ?? [],
          );
          const index = items.indexOf(
            document.activeElement as HTMLButtonElement,
          );
          const next =
            e.key === "Home"
              ? 0
              : e.key === "End"
                ? items.length - 1
                : ((index < 0 && e.key === "ArrowUp" ? 0 : index) + (e.key === "ArrowDown" ? 1 : -1) + items.length) %
                  items.length;
          items[next]?.focus();
        }
      }}
    >
      <button
        ref={trigger}
        className="select-trigger"
        disabled={disabled}
        aria-label={label}
        aria-expanded={open}
        aria-haspopup="listbox"
        aria-controls={open ? listId : undefined}
        onClick={() => { setFilter(""); setOpen(!open); }}
      >
        <span
          className="select-value"
          title={options.find((o) => o.value === value)?.label ?? label}
        >
          {options.find((o) => o.value === value)?.label ?? label}
        </span>
        <ChevronDown size={14} />
      </button>
      {open && (
        <div
          ref={popup}
          id={listId}
          className={floating ? "select-options select-options-floating" : "select-options"}
        >
          {searchOptions && <input type="search" aria-label={searchOptions} placeholder={searchOptions}
            autoFocus={!floating} value={filter} onChange={event => setFilter(event.target.value)} />}
          <div role="listbox" aria-label={label}>
          {visible.map((o) => (
            <button
              role="option"
              aria-selected={o.value === value}
              key={o.value}
              onClick={() => {
                onChange(o.value);
                setOpen(false);
                root.current?.querySelector("button")?.focus();
              }}
            >
              {o.label}
              {o.value === value && <Check size={14} />}
            </button>
          ))}
          </div>
          {!visible.length && <p role="status">Không có kết quả.</p>}
        </div>
      )}
    </div>
  );
}
export function Modal({
  title,
  children,
  onClose,
  wide = false,
  className = "",
}: {
  title: string;
  children: ReactNode;
  onClose: () => void;
  wide?: boolean;
  className?: string;
}) {
  const dialog = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  useEffect(() => {
    const d = dialog.current!;
    d.showModal();
    return () => d.close();
  }, []);
  return (
    <dialog
      ref={dialog}
      aria-labelledby={titleId}
      className={`modal ${wide ? "wide" : ""} ${className}`.trim()}
      onCancel={(e) => {
        e.preventDefault();
        onClose();
      }}
    >
      <div className="modal-heading">
        <h2 id={titleId}>{title}</h2>
        <IconButton label="Đóng" onClick={onClose}>
          <X size={18} />
        </IconButton>
      </div>
      {children}
    </dialog>
  );
}
export function DataGrid({
  table,
  onRow,
  selectedRows,
  onSelectionChange,
  onRowContextMenu,
  rangeSelection = false,
  followEnd = false,
  rowOffset = 0,
  onInspect,
}: {
  table: Table;
  onRow?: (row: (string | null)[]) => void;
  selectedRows?: number[];
  onSelectionChange?: (rows: number[]) => void;
  onRowContextMenu?: (row: number, event: React.MouseEvent<HTMLTableRowElement>) => void;
  rangeSelection?: boolean;
  followEnd?: boolean;
  rowOffset?: number;
  onInspect?: () => void;
}) {
  const [selection, setSelection] = useState<{
    r: number;
    c: number;
    endR: number;
    endC: number;
    kind: "cell" | "row" | "column";
  }>();
  const [copyError, setCopyError] = useState("");
  const grid = useRef<HTMLDivElement>(null);
  const drag = useRef<{ pointer: number; kind: "cell" | "row"; x: number; y: number }>();
  const dragFrame = useRef(0);
  const priorOffset = useRef(rowOffset);
  const rowHeight = 32;
  useEffect(() => () => cancelAnimationFrame(dragFrame.current), []);
  const select = (
    r: number,
    c: number,
    shift = false,
    kind: "cell" | "row" | "column" = "cell",
  ) =>
    setSelection((old) =>
      shift && old
        ? old.endR === r && old.endC === c ? old : { ...old, endR: r, endC: c }
        : { r, c, endR: r, endC: c, kind },
    );
  async function copy(kind = selection?.kind ?? "cell") {
    if (!selection) return;
    const r0 = kind === "column" ? 0 : Math.min(selection.r, selection.endR),
      r1 =
        kind === "column"
          ? table.rows.length - 1
          : Math.max(selection.r, selection.endR);
    const c0 = kind === "row" ? 0 : Math.min(selection.c, selection.endC),
      c1 =
        kind === "row"
          ? table.columns.length - 1
          : Math.max(selection.c, selection.endC);
    const quote = (v: string | null) =>
      /[\t\r\n"]/.test(v ?? "")
        ? '"' + (v ?? "").replaceAll('"', '""') + '"'
        : (v ?? "");
    try {
      await writeClipboard(
        table.rows
          .slice(r0, r1 + 1)
          .map((row) =>
            row
              .slice(c0, c1 + 1)
              .map(quote)
              .join("\t"),
          )
          .join("\r\n"),
      );
      setCopyError("");
    } catch (e) {
      setCopyError((e as Error).message);
    }
  }
  const isSelected = (r: number, c: number) =>
    selection &&
    (selection.kind === "column" ||
      (r >= Math.min(selection.r, selection.endR) &&
        r <= Math.max(selection.r, selection.endR))) &&
    (selection.kind === "row" ||
      (c >= Math.min(selection.c, selection.endC) &&
        c <= Math.max(selection.c, selection.endC)));
  useLayoutEffect(() => {
    const removed = rowOffset - priorOffset.current;
    priorOffset.current = rowOffset;
    if (!rangeSelection) setSelection(undefined);
    else if (removed > 0) {
      if (!followEnd && grid.current)
        grid.current.scrollTop = Math.max(0, grid.current.scrollTop - removed * rowHeight);
      setSelection((old) =>
        old && old.kind !== "column" && Math.min(old.r, old.endR) >= removed
          ? { ...old, r: old.r - removed, endR: old.endR - removed }
          : old?.kind === "column" ? old : undefined,
      );
    }
  }, [table, rangeSelection, rowOffset]);
  const [top, setTop] = useState(0);
  useLayoutEffect(() => {
    if (!followEnd || !grid.current) return;
    setSelection(undefined);
    grid.current.scrollTop = grid.current.scrollHeight;
    setTop(grid.current.scrollTop);
  }, [followEnd, table]);
  function cellAt(element: Element | null) {
    const cell = element?.closest<HTMLTableCellElement>("[data-grid-row]");
    if (!cell || !grid.current?.contains(cell)) return;
    return { row: Number(cell.dataset.gridRow), column: Number(cell.dataset.gridColumn) };
  }
  function extendDrag() {
    const active = drag.current;
    const element = grid.current;
    if (!active || !element) return;
    const rect = element.getBoundingClientRect();
    const right = rect.left + element.clientWidth;
    const bottom = rect.top + element.clientHeight;
    const topEdge = rect.top + rowHeight;
    const dx = active.x < rect.left + 12 ? -12 : active.x > right - 12 ? 12 : 0;
    const dy = active.y < topEdge + 12 ? -16 : active.y > bottom - 12 ? 16 : 0;
    const oldTop = element.scrollTop, oldLeft = element.scrollLeft;
    element.scrollBy(dx, dy);
    const x = Math.max(rect.left + 1, Math.min(right - 1, active.x));
    const y = Math.max(topEdge + 1, Math.min(bottom - 1, active.y));
    const cell = cellAt(document.elementFromPoint(x, y));
    if (cell) select(cell.row, active.kind === "row" ? 0 : Math.max(0, cell.column), true, active.kind);
    if (oldTop !== element.scrollTop || oldLeft !== element.scrollLeft)
      dragFrame.current = requestAnimationFrame(extendDrag);
  }
  const first = Math.min(
      Math.max(0, table.rows.length - 1),
      Math.max(0, Math.floor(top / rowHeight) - 5),
    ),
    last = Math.min(table.rows.length, first + 60);
  return (
    <div
      ref={grid}
      className={rangeSelection ? "data-grid data-grid-range" : "data-grid"}
      tabIndex={0}
      aria-label={rangeSelection
        ? (onInspect ? "Bảng sự kiện" : "Bảng dữ liệu") + " · Kéo chọn vùng, Ctrl+C sao chép"
        : "Bảng dữ liệu · Chọn ô, Shift chọn vùng, Ctrl+C sao chép"}
      onWheel={onInspect}
      onPointerDown={(e) => {
        if (!rangeSelection || e.button !== 0 || (e.target as Element).closest("button")) return;
        onInspect?.();
        const cell = cellAt(e.target as Element);
        if (!cell) return;
        e.preventDefault();
        e.currentTarget.focus({ preventScroll: true });
        e.currentTarget.setPointerCapture(e.pointerId);
        const kind = cell.column < 0 ? "row" : "cell";
        drag.current = { pointer: e.pointerId, kind, x: e.clientX, y: e.clientY };
        select(cell.row, Math.max(0, cell.column), e.shiftKey, kind);
        onRow?.(table.rows[cell.row]);
      }}
      onPointerMove={(e) => {
        const active = drag.current;
        if (active?.pointer !== e.pointerId) return;
        active.x = e.clientX;
        active.y = e.clientY;
        cancelAnimationFrame(dragFrame.current);
        extendDrag();
      }}
      onLostPointerCapture={() => {
        drag.current = undefined;
        cancelAnimationFrame(dragFrame.current);
      }}
      onKeyDown={(e) => {
        onInspect?.();
        if (
          (e.ctrlKey || e.metaKey) &&
          e.key.toLowerCase() === "c" &&
          selection
        ) {
          e.preventDefault();
          void copy();
        }
      }}
      onScroll={(e) => setTop(e.currentTarget.scrollTop)}
    >
      {selection && !rangeSelection && (
        <div className="grid-copy-menu">
          <Button onClick={() => void copy("cell")}>Sao chép ô / vùng</Button>
          <Button onClick={() => void copy("row")}>Sao chép dòng</Button>
          <Button onClick={() => void copy("column")}>Sao chép cột</Button>
          <span role="alert">{copyError}</span>
        </div>
      )}
      {rangeSelection && copyError && <span role="alert">{copyError}</span>}
      <table>
        <thead>
          <tr>
            <th className="row-number">
              {onSelectionChange ? (
                <button
                  className="grid-row-select"
                  aria-label="Chọn tất cả dòng trang"
                  aria-pressed={selectedRows?.length === table.rows.length}
                  onClick={() =>
                    onSelectionChange(
                      selectedRows?.length === table.rows.length
                        ? []
                        : table.rows.map((_, i) => i),
                    )
                  }
                >
                  #
                </button>
              ) : (
                "#"
              )}
            </th>
            {table.columns.map((c, i) => (
              <th
                key={i}
                tabIndex={0}
                title="Chọn cột · Ctrl+C sao chép"
                onClick={(e) => select(0, i, e.shiftKey, "column")}
                onKeyDown={(e) => {
                  if (e.key === "Enter") select(0, i, false, "column");
                }}
                className={
                  selection?.kind === "column" && isSelected(0, i)
                    ? "grid-selected"
                    : undefined
                }
              >
                {c}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {first > 0 && (
            <tr style={{ height: first * rowHeight }} aria-hidden>
              <td colSpan={table.columns.length + 1} />
            </tr>
          )}
          {table.rows.slice(first, last).map((row, i) => (
            <tr
              key={first + i}
              aria-selected={
                followEnd && first + i === table.rows.length - 1
                  ? true
                  : onSelectionChange
                  ? selectedRows?.includes(first + i)
                  : undefined
              }
              onDoubleClick={() => onRow?.(row)}
              onContextMenu={(event) => onRowContextMenu?.(first + i, event)}
              tabIndex={onRow ? 0 : undefined}
              onKeyDown={(e) => {
                if (e.key === "Enter") onRow?.(row);
              }}
            >
              <td
                className="row-number"
                data-grid-row={rangeSelection ? first + i : undefined}
                data-grid-column={rangeSelection ? -1 : undefined}
                title="Chọn dòng · Ctrl+C sao chép"
                onClick={(e) => { if (!rangeSelection || onSelectionChange) select(first + i, 0, e.shiftKey, "row"); }}
              >
                {onSelectionChange ? (
                  <button
                    className="grid-row-select"
                    aria-label={`Chọn dòng ${first + i + 1}`}
                    aria-pressed={selectedRows?.includes(first + i)}
                    onClick={() =>
                      onSelectionChange(
                        selectedRows?.includes(first + i)
                          ? selectedRows.filter((n) => n !== first + i)
                          : [...(selectedRows ?? []), first + i],
                      )
                    }
                  >
                    {selectedRows?.includes(first + i) ? (
                      <Check size={12} />
                    ) : (
                      first + i + 1
                    )}
                  </button>
                ) : (
                  first + i + 1
                )}
              </td>
              {row.map((v, k) => (
                <td
                  key={k}
                  data-grid-row={rangeSelection ? first + i : undefined}
                  data-grid-column={rangeSelection ? k : undefined}
                  title={v ?? "NULL"}
                  tabIndex={0}
                  className={
                    isSelected(first + i, k) ? "grid-selected" : undefined
                  }
                  onClick={(e) => { if (!rangeSelection) select(first + i, k, e.shiftKey); }}
                  onKeyDown={(e) => {
                    if (e.key === "Enter") select(first + i, k, e.shiftKey);
                  }}
                >
                  {v === null ? <span className="muted">NULL</span> : v}
                </td>
              ))}
            </tr>
          ))}
          {last < table.rows.length && (
            <tr
              style={{ height: (table.rows.length - last) * rowHeight }}
              aria-hidden
            >
              <td colSpan={table.columns.length + 1} />
            </tr>
          )}
        </tbody>
      </table>
      {table.rows.length === 0 && (
        <div className="empty">Không có dòng dữ liệu.</div>
      )}
    </div>
  );
}
