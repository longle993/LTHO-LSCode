import { useRef, useState } from "react";
import { Play } from "lucide-react";
import { Button, DataGrid } from "../ui";
import { request, operation, quoteSqlName } from "../bridge";
import type { Table } from "../bridge";
import type { ToolProps } from "./types";
import { useTask } from "./useTask";
export function TableTool({ target, onText }: ToolProps) {
  const [schema, setSchema] = useState("dbo"),
    [table, setTable] = useState(""),
    [where, setWhere] = useState(""),
    [orderBy, setOrderBy] = useState(""),
    [columns, setColumns] = useState(""),
    [mode, setMode] = useState("data"),
    [result, setResult] = useState<Table>();
  const task = useTask();
  const op = useRef<ReturnType<typeof operation<Table>>>();
  return (
    <div className="tool-body large">
      <p className="muted">
        {target
          ? `${target.server} / ${target.database}`
          : "Chọn kết nối SQL trong tab trước khi mở bảng."}{" "}
        · Chỉ đọc · Tối đa 1000 dòng
      </p>
      <div className="form-row">
        <label className="field">
          Schema
          <input value={schema} onChange={(e) => setSchema(e.target.value)} />
        </label>
        <label className="field">
          Bảng
          <input value={table} onChange={(e) => setTable(e.target.value)} />
        </label>
      </div>
      <div className="tool-toolbar">
        <Button
          variant={mode === "data" ? "secondary" : "ghost"}
          onClick={() => setMode("data")}
        >
          Dữ liệu
        </Button>
        <Button
          variant={mode === "structure" ? "secondary" : "ghost"}
          onClick={() => setMode("structure")}
        >
          Cấu trúc
        </Button>
        <Button
          onClick={() =>
            void task.run(async () =>
              onText(
                await request<string>("schema.script", {
                  targetId: target?.id,
                  object: quoteSqlName(schema, table),
                }),
                table + ".sql",
              ),
            )
          }
        >
          Script
        </Button>
      </div>
      {mode === "data" && (
        <div className="tool-toolbar">
          <input
            aria-label="Cột SELECT"
            placeholder="Cột, cách nhau bằng dấu phẩy; trống = tất cả"
            value={columns}
            onChange={(e) => setColumns(e.target.value)}
          />
          <input
            aria-label="WHERE"
            placeholder="WHERE (không gồm từ khóa)"
            value={where}
            onChange={(e) => setWhere(e.target.value)}
          />
          <input
            aria-label="ORDER BY"
            placeholder="ORDER BY"
            value={orderBy}
            onChange={(e) => setOrderBy(e.target.value)}
          />
        </div>
      )}
      <div className="tool-toolbar">
        <Button
          variant="primary"
          disabled={!target || !table || task.busy}
          onClick={() =>
            void task.run(async () => {
              op.current = operation<Table>(
                mode === "data" ? "table.read" : "schema.columns",
                {
                  targetId: target?.id,
                  schema,
                  table,
                  object: quoteSqlName(schema, table),
                  where,
                  orderBy,
                  columns: columns
                    .split(",")
                    .map((x) => x.trim())
                    .filter(Boolean),
                },
              );
              setResult(await op.current.promise);
            })
          }
        >
          <Play size={14} />
          {mode === "data" ? "Đọc dữ liệu" : "Đọc cấu trúc"}
        </Button>
        {task.busy && (
          <Button onClick={() => void op.current?.cancel()}>Dừng</Button>
        )}
      </div>
      <p className="form-error">{task.error}</p>
      {result ? (
        <DataGrid table={result} />
      ) : (
        <div className="empty">Chọn bảng để xem cấu trúc hoặc dữ liệu.</div>
      )}
    </div>
  );
}
