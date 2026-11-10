import { writeClipboard } from "../bridge";
import { useEffect, useRef, useState } from "react";
import { Copy } from "lucide-react";
import { Button, Select } from "../ui";
import { request, operation } from "../bridge";
import type { ToolProps } from "./types";
import { useTask } from "./useTask";
export function TranslationTool({ selectedText, onInsert }: ToolProps) {
  const [text, setText] = useState(selectedText ?? ""),
    [result, setResult] = useState(""),
    [from, setFrom] = useState("auto"),
    [to, setTo] = useState(
      /[ăâđêôơưà-ỹ]/i.test(selectedText ?? "") ? "en" : "vi",
    );
  const task = useTask();
  const op = useRef<ReturnType<typeof operation<string>>>();
  const latest = useRef("");
  latest.current = JSON.stringify([text, from, to]);
  useEffect(() => {
    setResult("");
    void op.current?.cancel().catch(() => {});
  }, [text, from, to]);
  useEffect(
    () => () => {
      void op.current?.cancel().catch(() => {});
    },
    [],
  );
  return (
    <div className="tool-body large">
      <div className="tool-toolbar">
        <Select
          label="Ngôn ngữ nguồn"
          value={from}
          options={[
            "auto",
            "vi",
            "en",
            "ja",
            "ko",
            "zh-CN",
            "zh-TW",
            "fr",
            "de",
            "th",
            "lo",
            "ru",
          ].map((v) => ({ value: v, label: v }))}
          onChange={setFrom}
        />
        <Button
          onClick={() => {
            setFrom(to);
            setTo(from === "auto" ? "en" : from);
            setText(result);
            setResult(text);
          }}
        >
          Đổi chiều
        </Button>
        <Select
          label="Ngôn ngữ đích"
          value={to}
          options={[
            "vi",
            "en",
            "ja",
            "ko",
            "zh-CN",
            "zh-TW",
            "fr",
            "de",
            "th",
            "lo",
            "ru",
          ].map((v) => ({ value: v, label: v }))}
          onChange={setTo}
        />
        <Button
          variant="primary"
          disabled={task.busy || !text}
          onClick={() =>
            void task.run(async () => {
              const key = JSON.stringify([text, from, to]);
              op.current = operation<string>("translation.run", {
                text,
                from,
                to,
              });
              try {
                const translated = await op.current.promise;
                if (key === latest.current) setResult(translated);
              } catch (e) {
                if (key === latest.current) throw e;
              }
            })
          }
        >
          Dịch
        </Button>
        {task.busy && (
          <Button onClick={() => void op.current?.cancel()}>Dừng</Button>
        )}
      </div>
      <p className="muted">
        Nội dung được gửi tới dịch vụ Google khi nhấn Dịch. Tối đa 4500 ký tự.
      </p>
      <div className="tool-columns">
        <textarea
          aria-label="Văn bản nguồn"
          className="tool-text"
          value={text}
          onChange={(e) => setText(e.target.value)}
        />
        <textarea
          aria-label="Bản dịch"
          className="tool-text"
          value={result}
          readOnly
        />
      </div>
      <div className="tool-toolbar">
        <Button onClick={() => void writeClipboard(result)}>
          <Copy size={14} />
          Sao chép
        </Button>
        <Button onClick={() => onInsert(result)}>Chèn bản dịch</Button>
        <Button
          onClick={() =>
            void task.run(async () => {
              await request("translation.browser", { text, from, to });
            })
          }
        >
          Mở Google Dịch
        </Button>
      </div>
      <p className="form-error">{task.error}</p>
    </div>
  );
}
