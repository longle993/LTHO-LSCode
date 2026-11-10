import { useState } from "react";
import { Button } from "../ui";
import { request } from "../bridge";
import type { ToolProps } from "./types";
import { useTask } from "./useTask";
export function XmlTool({ getText, document, onOpen }: ToolProps) {
  const [expression, setExpression] = useState("//*"),
    [output, setOutput] = useState(""),
    [outline, setOutline] = useState<{ name: string; line: number }[]>([]),
    [offset, setOffset] = useState("0");
  const task = useTask();
  return (
    <div className="tool-body large">
      <p className="muted">
        XML của tab hiện tại · namespace mặc định dùng prefix d
      </p>
      <div className="tool-toolbar">
        <Button
          onClick={() =>
            void task.run(async () => {
              const r = await request<{
                diagnostic: {
                  message: string;
                  line: number;
                  column: number;
                } | null;
                outline: { name: string; line: number }[];
              }>("xml.analyze", { text: getText() });
              setOutline(r.outline);
              setOutput(
                r.diagnostic
                  ? `${r.diagnostic.line}:${r.diagnostic.column} ${r.diagnostic.message}`
                  : "XML hợp lệ.",
              );
            })
          }
        >
          Kiểm tra & Outline
        </Button>
        <input
          aria-label="XPath"
          value={expression}
          onChange={(e) => setExpression(e.target.value)}
        />
        <Button
          onClick={() =>
            void task.run(async () =>
              setOutput(
                await request<string>("xml.xpath", {
                  text: getText(),
                  expression,
                }),
              ),
            )
          }
        >
          XPath
        </Button>
      </div>
      <div className="tool-toolbar">
        <input
          aria-label="Vị trí ký tự include"
          type="number"
          value={offset}
          onChange={(e) => setOffset(e.target.value)}
        />
        <Button
          onClick={() =>
            void task.run(async () => {
              const r = await request<{
                path: string;
                line: number;
                column: number;
              } | null>("xml.include", {
                path: document?.path,
                text: getText(),
                offset: Number(offset),
              });
              if (r) onOpen(r.path);
              else setOutput("Không tìm thấy include tại vị trí này.");
            })
          }
        >
          Mở include tại offset
        </Button>
      </div>
      <div className="tool-columns">
        <div className="tool-list">
          {outline.map((o, i) => (
            <div key={i} className="section-label">
              {o.line} · {o.name}
            </div>
          ))}
        </div>
        <textarea
          aria-label="Kết quả XML"
          className="tool-text"
          readOnly
          value={output}
        />
      </div>
      <p className="form-error">{task.error}</p>
    </div>
  );
}
