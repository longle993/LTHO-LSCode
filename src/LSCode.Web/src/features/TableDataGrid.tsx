import { useEffect, useState } from "react";
import { KeyRound } from "lucide-react";
import type { Table } from "../bridge";

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

export function TableDataGrid({
  table,
  metadata,
  readonly,
  busy,
  onCommit,
  emptyMessage = "Chưa có dữ liệu. Nhập ở dòng * rồi nhấn Enter để thêm.",
}: {
  table: Table;
  metadata: TableColumn[];
  readonly: boolean;
  busy: boolean;
  onCommit: (row: number, values: (string | null)[]) => void;
  emptyMessage?: string;
}) {
  const [drafts, setDrafts] = useState<Record<string, (string | null)[]>>({});
  const [showLoading, setShowLoading] = useState(false);
  useEffect(() => setDrafts({}), [table]);
  useEffect(() => {
    if (!busy) {
      setShowLoading(false);
      return;
    }
    const timer = window.setTimeout(() => setShowLoading(true), 250);
    return () => window.clearTimeout(timer);
  }, [busy]);

  const safeColumns = Array.isArray(table.columns) ? table.columns : [];
  const safeRows = Array.isArray(table.rows) ? table.rows.filter((row): row is (string | null)[] => Array.isArray(row)) : [];
  const safeMetadata = Array.isArray(metadata) ? metadata : [];
  const columns = safeColumns.map((name) => safeMetadata.find((column) =>
    column.name.toLowerCase() === name.toLowerCase()));
  const keyVisible = safeMetadata.filter((column) => column.primaryKey)
    .every((column) => safeColumns.some((name) => name.toLowerCase() === column.name.toLowerCase()));
  const canUpdate = keyVisible && safeMetadata.some((column) => column.primaryKey);
  const valueAt = (row: number, column: number) => {
    const draft = drafts[String(row)];
    if (draft) return draft[column] ?? "";
    if (row >= 0 && row < safeRows.length) return safeRows[row]?.[column] ?? "";
    return "";
  };
  function edit(row: number, column: number, value: string) {
    setDrafts((old) => {
      const next = { ...old };
      const current = next[String(row)] ?? (row >= 0 && row < safeRows.length ? [...safeRows[row]] : safeColumns.map(() => ""));
      current[column] = value;
      next[String(row)] = current;
      return next;
    });
  }
  function commit(row: number) {
    if (readonly || busy) return;
    const values = drafts[String(row)];
    if (!values || (row < 0 && values.every((value) => value === "" || value == null))) return;
    onCommit(row, values);
    setDrafts((old) => {
      const next = { ...old };
      delete next[String(row)];
      return next;
    });
  }
  return (
    <div className="table-data-grid data-grid" aria-label="Dữ liệu bảng" aria-busy={busy} data-loading={showLoading ? "true" : undefined}>
      {showLoading && <div className="table-data-loading" role="status">Đang lọc dữ liệu…</div>}
      {!readonly && !canUpdate && safeRows.length > 0 && (
        <p className="table-grid-warning" role="status">
          Bảng không có đủ khóa chính trong kết quả hiện tại; chỉ có thể thêm dòng mới.
        </p>
      )}
      <table>
        <thead>
          <tr>
            <th className="row-number">#</th>
            {safeColumns.map((name, index) => {
              const column = columns[index];
              return <th key={name + index} title={column?.sqlType ?? name} className={column?.key ? "table-key-column" : undefined}>
                {column?.key && <KeyRound size={13} aria-label={column.primaryKey ? "Khóa chính" : "Khóa"} />}
                {name}
              </th>;
            })}
          </tr>
        </thead>
        <tbody>
          {safeRows.map((row, rowIndex) => (
            <tr key={rowIndex}>
              <td className="row-number">{rowIndex + 1}</td>
              {row.map((value, columnIndex) => {
                const column = columns[columnIndex];
                const editable = !readonly && canUpdate && !!column?.writable && !column.primaryKey;
                return <td key={columnIndex} title={value ?? "NULL"} className={column?.key ? "table-key-column" : undefined}>
                  {editable ? <input
                    aria-label={`${safeColumns[columnIndex] ?? "Cột"} dòng ${rowIndex + 1}`}
                    value={String(valueAt(rowIndex, columnIndex))}
                    disabled={busy}
                    onChange={(event) => edit(rowIndex, columnIndex, event.target.value)}
                    onBlur={() => commit(rowIndex)}
                    onKeyDown={(event) => {
                      if (event.key === "Enter") {
                        event.preventDefault();
                        commit(rowIndex);
                      }
                    }}
                  /> : value == null ? <span className="muted">NULL</span> : value}
                </td>;
              })}
            </tr>
          ))}
          <tr className="table-new-row">
            <td className="row-number" title="Dòng mới">*</td>
            {safeColumns.map((name, columnIndex) => {
              const column = columns[columnIndex];
              const editable = !readonly && !!column?.writable;
              return <td key={name + columnIndex} className={column?.key ? "table-key-column" : undefined}>
                {editable ? <input
                  aria-label={`${name} dòng mới`}
                  placeholder={column?.nullable ? "NULL" : "Nhập giá trị"}
                  value={String(valueAt(-1, columnIndex))}
                  disabled={busy}
                  onChange={(event) => edit(-1, columnIndex, event.target.value)}
                  onKeyDown={(event) => {
                    if (event.key === "Enter") {
                      event.preventDefault();
                      commit(-1);
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
