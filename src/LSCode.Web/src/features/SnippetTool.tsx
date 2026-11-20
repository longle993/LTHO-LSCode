import { useState } from "react";
import { Trash2 } from "lucide-react";
import { Button, IconButton } from "../ui";
import { request } from "../bridge";
import type { Snippet } from "../bridge";
import type { ToolProps } from "./types";
import { useTask } from "./useTask";
export function SnippetTool({ snippets, onSnippets, onInsert }: ToolProps) {
  const [selected, setSelected] = useState(-1),
    [query, setQuery] = useState(""),
    [draft, setDraft] = useState<Snippet>({
      key: "",
      category: "User",
      body: "",
      description: "",
      position: 0,
      param: "",
    }),
    [xml, setXml] = useState(""),
    [transfer, setTransfer] = useState(false);
  const task = useTask();
  async function persist(values: Snippet[]) {
    onSnippets(await request<Snippet[]>("snippet.save", { snippets: values }));
  }
  return (
    <div className="tool-body large">
      <div className="tool-toolbar">
        <input
          aria-label="Tìm snippet"
          placeholder="Tìm snippet"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
        />
        <Button
          onClick={() => {
            setSelected(-1);
            setDraft({
              key: "",
              category: "User",
              body: "",
              description: "",
              position: 0,
              param: "",
            });
          }}
        >
          Tạo mới
        </Button>
        <Button onClick={() => setTransfer(!transfer)}>Import / Export</Button>
      </div>
      {transfer ? (
        <>
          <textarea
            aria-label="Snippet XML"
            className="tool-text"
            value={xml}
            onChange={(e) => setXml(e.target.value)}
          />
          <div className="tool-toolbar">
            <Button
              onClick={() =>
                void task.run(async () =>
                  setXml(await request<string>("snippet.export")),
                )
              }
            >
              Export XML
            </Button>
            <Button
              onClick={() =>
                void task.run(async () => {
                  const values = await request<Snippet[]>("snippet.import", {
                    xml,
                  });
                  const merged = [...snippets];
                  for (const v of values)
                    if (
                      !merged.some(
                        (x) => x.key === v.key && x.category === v.category,
                      )
                    )
                      merged.push(v);
                  await persist(merged);
                  setTransfer(false);
                })
              }
            >
              Import · giữ bản trùng
            </Button>
          </div>
        </>
      ) : (
        <div className="tool-columns">
          <div className="tool-list">
            {snippets
              .map((s, i) => ({ ...s, i }))
              .filter((s) =>
                (s.key + " " + s.category)
                  .toLowerCase()
                  .includes(query.toLowerCase()),
              )
              .map((s) => (
                <button
                  key={s.i}
                  className={selected === s.i ? "selected" : ""}
                  onClick={() => {
                    setSelected(s.i);
                    setDraft({ ...s });
                  }}
                >
                  <span>
                    <strong>{s.key}</strong>
                    <small>
                      {s.category} · {s.description}
                    </small>
                  </span>
                </button>
              ))}
          </div>
          <div>
            <div className="form-row">
              <label className="field">
                Từ khóa nhanh
                <input
                  placeholder="Ví dụ: /1"
                  value={draft.key}
                  onChange={(e) => setDraft({ ...draft, key: e.target.value })}
                />
              </label>
              <label className="field">
                Nhóm
                <input
                  value={draft.category}
                  onChange={(e) =>
                    setDraft({ ...draft, category: e.target.value })
                  }
                />
              </label>
            </div>
            <label className="field">
              Mô tả
              <input
                value={draft.description}
                onChange={(e) =>
                  setDraft({ ...draft, description: e.target.value })
                }
              />
            </label>
            <div className="form-row">
              <label className="field">
                Vị trí con trỏ
                <input
                  type="number"
                  min="0"
                  max={draft.body.length}
                  value={draft.position}
                  onChange={(e) =>
                    setDraft({ ...draft, position: Number(e.target.value) })
                  }
                />
              </label>
              <label className="field">
                Tham số
                <input
                  value={draft.param}
                  onChange={(e) =>
                    setDraft({ ...draft, param: e.target.value })
                  }
                />
              </label>
            </div>
            <textarea
              aria-label="Nội dung snippet"
              className="tool-text"
              value={draft.body}
              onChange={(e) => setDraft({ ...draft, body: e.target.value })}
            />
            <div className="tool-toolbar">
              <Button
                variant="primary"
                onClick={() => onInsert(draft.body, draft.position)}
              >
                Chèn vào editor
              </Button>
              <Button
                disabled={!draft.key || task.busy}
                onClick={() =>
                  void task.run(() =>
                    persist(
                      selected < 0
                        ? [...snippets, draft]
                        : snippets.map((s, i) => (i === selected ? draft : s)),
                    ),
                  )
                }
              >
                Lưu mẫu
              </Button>
              <IconButton
                label="Xóa mẫu"
                disabled={selected < 0}
                onClick={() =>
                  void task.run(async () => {
                    if (window.confirm("Xóa snippet " + draft.key + "?")) {
                      await persist(snippets.filter((_, i) => i !== selected));
                      setSelected(-1);
                    }
                  })
                }
              >
                <Trash2 size={15} />
              </IconButton>
            </div>
          </div>
        </div>
      )}
      <p className="form-error">{task.error}</p>
    </div>
  );
}
