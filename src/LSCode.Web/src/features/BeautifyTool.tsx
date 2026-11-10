import { writeClipboard } from "../bridge";
import { useState } from "react";
import { Button } from "../ui";
import { request } from "../bridge";
import type { ToolProps } from "./types";
import { useTask } from "./useTask";
export function BeautifyTool({ getText, onText }: ToolProps) {
  const [text, setText] = useState(getText());
  const task = useTask();
  return (
    <div className="tool-body large">
      <textarea
        aria-label="SQL cần định dạng"
        className="tool-text"
        value={text}
        onChange={(e) => setText(e.target.value)}
      />
      <div className="tool-toolbar">
        <Button
          variant="primary"
          disabled={task.busy}
          onClick={() =>
            void task.run(async () =>
              setText(
                await request<string>("text.format", { language: "sql", text }),
              ),
            )
          }
        >
          Định dạng SQL
        </Button>
        <Button onClick={() => onText(text, "formatted.sql")}>
          Mở trong editor
        </Button>
        <Button onClick={() => void writeClipboard(text)}>Sao chép</Button>
      </div>
      <p className="form-error">{task.error}</p>
    </div>
  );
}
