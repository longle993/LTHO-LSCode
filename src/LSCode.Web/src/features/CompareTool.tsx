import { useState, useRef, useEffect } from "react";
import { Button, DataGrid } from "../ui";
import { request, operation } from "../bridge";
import type { ToolProps } from "./types";
import { useTask } from "./useTask";
export function CompareTool({ getText, onOpen }: ToolProps) {
  const [left, setLeft] = useState(getText()),
    [right, setRight] = useState(""),
    [mode, setMode] = useState("text"),
    [rows, setRows] = useState<
      {
        leftLine: number | null;
        rightLine: number | null;
        kind: string;
        left: string;
        right: string;
      }[]
    >([]),
    [folderResult, setFolderResult] = useState<
      {
        relativePath: string;
        status: string;
        leftPath: string;
        rightPath: string;
        detail: string;
      }[]
    >([]);
  const task = useTask();
  const pending = useRef<{ cancel: () => Promise<unknown> }>();
  useEffect(
    () => () => {
      void pending.current?.cancel().catch(() => {});
    },
    [],
  );
  return (
    <div className="tool-body large">
      <div className="tool-toolbar">
        <Button
          variant={mode === "text" ? "secondary" : "ghost"}
          onClick={() => setMode("text")}
        >
          Văn bản
        </Button>
        <Button
          variant={mode === "folder" ? "secondary" : "ghost"}
          onClick={() => {
            setMode("folder");
            setLeft("");
            setRight("");
          }}
        >
          Thư mục
        </Button>
        <Button
          variant="primary"
          disabled={task.busy}
          onClick={() =>
            void task.run(async () => {
              if (mode === "text")
                setRows(await request("diff.text", { left, right }));
              else {
                const op = operation<typeof folderResult>("diff.folder", {
                  left,
                  right,
                });
                pending.current = op;
                setFolderResult(await op.promise);
              }
            })
          }
        >
          So sánh
        </Button>
        {task.busy && mode === "folder" && (
          <Button onClick={() => void pending.current?.cancel()}>Dừng</Button>
        )}
        {mode === "text" && (
          <Button
            onClick={() =>
              void task.run(async () => {
                const d = await request<
                  import("../bridge").DocumentInfo | null
                >("document.open");
                if (d) {
                  setRight(d.text);
                  await request("document.close", { id: d.id });
                }
              })
            }
          >
            So với file…
          </Button>
        )}
      </div>
      <div className="tool-columns">
        <div>
          <label className="field">
            Bên trái
            {mode === "text" ? (
              <textarea
                className="tool-text"
                value={left}
                onChange={(e) => setLeft(e.target.value)}
              />
            ) : (
              <input value={left} onChange={(e) => setLeft(e.target.value)} />
            )}
          </label>
          {mode === "folder" && (
            <Button
              onClick={() =>
                void task.run(async () => {
                  const p = await request<string | null>("workspace.folder");
                  if (p) setLeft(p);
                })
              }
            >
              Chọn thư mục
            </Button>
          )}
        </div>
        <div>
          <label className="field">
            Bên phải
            {mode === "text" ? (
              <textarea
                className="tool-text"
                value={right}
                onChange={(e) => setRight(e.target.value)}
              />
            ) : (
              <input value={right} onChange={(e) => setRight(e.target.value)} />
            )}
          </label>
          {mode === "folder" && (
            <Button
              onClick={() =>
                void task.run(async () => {
                  const p = await request<string | null>("workspace.folder");
                  if (p) setRight(p);
                })
              }
            >
              Chọn thư mục
            </Button>
          )}
        </div>
      </div>
      {mode === "text" && rows.length > 0 && (
        <div className="diff-lines">
          {rows.map((r, i) => (
            <div
              className={
                r.kind === "+" ? "added" : r.kind === "−" ? "removed" : ""
              }
              key={i}
            >
              <span>{r.kind}</span>
              <pre>
                {r.leftLine} {r.left}
              </pre>
              <pre>
                {r.rightLine} {r.right}
              </pre>
            </div>
          ))}
        </div>
      )}
      {mode === "folder" && folderResult.length > 0 && (
        <DataGrid
          table={{
            columns: ["File", "Trạng thái", "Bên trái", "Bên phải", "Chi tiết"],
            rows: folderResult.map((r) => [
              r.relativePath,
              r.status,
              r.leftPath,
              r.rightPath,
              r.detail,
            ]),
          }}
          onRow={(r) => {
            if (r[2]) onOpen(r[2]);
          }}
        />
      )}
      <p className="form-error">{task.error}</p>
    </div>
  );
}
