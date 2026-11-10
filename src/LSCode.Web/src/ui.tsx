import { writeClipboard } from "./bridge";
import { useEffect, useRef, useState, useId } from "react";
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
}: {
  value: string;
  options: { value: string; label: string }[];
  onChange: (v: string) => void;
  label: string;
  disabled?: boolean;
}) {
  const [open, setOpen] = useState(false);
  const listId = useId();
  const root = useRef<HTMLDivElement>(null);
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
        if (e.key === "Escape") {
          setOpen(false);
          root.current?.querySelector("button")?.focus();
        }
        if (
          e.key === "ArrowDown" ||
          e.key === "ArrowUp" ||
          e.key === "Home" ||
          e.key === "End"
        ) {
          e.preventDefault();
          if (!open) {
            setOpen(true);
            requestAnimationFrame(() =>
              root.current
                ?.querySelector<HTMLButtonElement>("[role=option]")
                ?.focus(),
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
                : (index + (e.key === "ArrowDown" ? 1 : -1) + items.length) %
                  items.length;
          items[next]?.focus();
        }
      }}
    >
      <button
        className="select-trigger"
        disabled={disabled}
        aria-label={label}
        aria-expanded={open}
        aria-haspopup="listbox"
        aria-controls={open ? listId : undefined}
        onClick={() => setOpen(!open)}
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
          id={listId}
          className="select-options"
          role="listbox"
          aria-label={label}
        >
          {options.map((o) => (
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
      )}
    </div>
  );
}
export function Modal({
  title,
  children,
  onClose,
  wide = false,
}: {
  title: string;
  children: ReactNode;
  onClose: () => void;
  wide?: boolean;
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
      className={`modal ${wide ? "wide" : ""}`}
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
}: {
  table: Table;
  onRow?: (row: (string | null)[]) => void;
  selectedRows?: number[];
  onSelectionChange?: (rows: number[]) => void;
}) {
  const [selection, setSelection] = useState<{
    r: number;
    c: number;
    endR: number;
    endC: number;
    kind: "cell" | "row" | "column";
  }>();
  const [copyError, setCopyError] = useState("");
  const select = (
    r: number,
    c: number,
    shift = false,
    kind: "cell" | "row" | "column" = "cell",
  ) =>
    setSelection((old) =>
      shift && old
        ? { ...old, endR: r, endC: c }
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
  useEffect(() => setSelection(undefined), [table]);
  const [top, setTop] = useState(0);
  const rowHeight = 32;
  const first = Math.min(
      Math.max(0, table.rows.length - 1),
      Math.max(0, Math.floor(top / rowHeight) - 5),
    ),
    last = Math.min(table.rows.length, first + 60);
  return (
    <div
      className="data-grid"
      tabIndex={0}
      aria-label="Bảng dữ liệu · Chọn ô, Shift chọn vùng, Ctrl+C sao chép"
      onKeyDown={(e) => {
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
      {selection && (
        <div className="grid-copy-menu">
          <Button onClick={() => void copy("cell")}>Sao chép ô / vùng</Button>
          <Button onClick={() => void copy("row")}>Sao chép dòng</Button>
          <Button onClick={() => void copy("column")}>Sao chép cột</Button>
          <span role="alert">{copyError}</span>
        </div>
      )}
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
                onSelectionChange
                  ? selectedRows?.includes(first + i)
                  : undefined
              }
              onDoubleClick={() => onRow?.(row)}
              tabIndex={onRow ? 0 : undefined}
              onKeyDown={(e) => {
                if (e.key === "Enter") onRow?.(row);
              }}
            >
              <td
                className="row-number"
                title="Chọn dòng · Ctrl+C sao chép"
                onClick={(e) => select(first + i, 0, e.shiftKey, "row")}
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
                  title={v ?? "NULL"}
                  tabIndex={0}
                  className={
                    isSelected(first + i, k) ? "grid-selected" : undefined
                  }
                  onClick={(e) => select(first + i, k, e.shiftKey)}
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
