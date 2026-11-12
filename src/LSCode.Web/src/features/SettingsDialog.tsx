import { useState } from "react";
import { Button, Modal, Select } from "../ui";
import type { Settings } from "../bridge";
import { useTask } from "./useTask";
export function SettingsDialog({
  settings,
  commands,
  onSave,
  onClose,
}: {
  settings: Settings;
  commands: string[][];
  onSave: (s: Settings) => Promise<void>;
  onClose: () => void;
}) {
  const [draft, setDraft] = useState({
      ...settings,
      shortcuts: { ...settings.shortcuts },
    }),
    [section, setSection] = useState("general");
  const task = useTask();
  function validate() {
    const used = new Map<string, string>();
    for (const [id, label, key] of commands) {
      for (const gesture of draft.shortcuts[id] ?? [key]) {
        if (!gesture) continue;
        const normalized = gesture.toUpperCase();
        if (used.has(normalized))
          throw new Error(
            `Phím ${gesture} trùng giữa ${label} và ${used.get(normalized)}.`,
          );
        used.set(normalized, label);
      }
    }
  }
  return (
    <Modal title="Cài đặt không gian làm việc" onClose={onClose} wide>
      <div className="tool-body">
        <div className="tool-toolbar">
          <Button
            variant={section === "general" ? "secondary" : "ghost"}
            onClick={() => setSection("general")}
          >
            Giao diện & công cụ
          </Button>
          <Button
            variant={section === "activity" ? "secondary" : "ghost"}
            onClick={() => setSection("activity")}
          >
            Lối tắt bên trái
          </Button>
          <Button
            variant={section === "shortcuts" ? "secondary" : "ghost"}
            onClick={() => setSection("shortcuts")}
          >
            Phím tắt
          </Button>
        </div>
        {section === "general" ? (
          <div className="form">
            <label>
              Theme
              <Select
                label="Theme"
                value={draft.theme}
                options={["Light", "Dark", "High Contrast"].map((v) => ({
                  value: v,
                  label: v,
                }))}
                onChange={(v) => setDraft({ ...draft, theme: v })}
              />
            </label>
            <div className="inline-checks">
              <label>
                Màu nhấn
                <input type="color" aria-label="Màu nhấn" value={draft.accentColor || (
                  draft.theme === "Light" ? "#2563a6" : draft.theme === "High Contrast" ? "#ffff00" : "#88b9ef"
                )}
                  onChange={(event) => setDraft({ ...draft, accentColor: event.target.value })} />
              </label>
              <Button type="button" variant="ghost" disabled={!draft.accentColor}
                onClick={() => setDraft({ ...draft, accentColor: null })}>Màu mặc định</Button>
            </div>
            <label>
              Cỡ chữ Không gian dự án
              <Select label="Cỡ chữ Không gian dự án" value={String(draft.explorerFontSize ?? 12)}
                options={Array.from({ length: 9 }, (_, index) => ({ value: String(index + 11), label: (index + 11) + " px" }))}
                onChange={(value) => setDraft({ ...draft, explorerFontSize: Number(value) })} />
            </label>
            <label>
              Cỡ chữ editor
              <Select label="Cỡ chữ editor" value={String(draft.editorFontSize ?? 14)}
                options={Array.from({ length: 15 }, (_, index) => ({ value: String(index + 10), label: (index + 10) + " px" }))}
                onChange={(value) => setDraft({ ...draft, editorFontSize: Number(value) })} />
            </label>
            <label>
              Crystal Reports executable
              <input
                value={draft.crystalPath}
                onChange={(e) =>
                  setDraft({ ...draft, crystalPath: e.target.value })
                }
              />
            </label>
            <label>
              SSMS 18 Profiler executable
              <input
                value={draft.profiler18Path}
                onChange={(e) =>
                  setDraft({ ...draft, profiler18Path: e.target.value })
                }
              />
            </label>
            <p className="muted">
              Giữ dữ liệu tại %LocalAppData%\LSCode hoặc LSCODE_DATA_HOME.
              WebView không lưu mật khẩu trong trình duyệt.
            </p>
          </div>
        ) : section === "activity" ? (
          <ActivitySettings
            commands={commands}
            value={
              draft.activityCommands ?? [
                "profiler",
                "compare",
                "snippets",
                "translation",
              ]
            }
            onChange={(value) =>
              setDraft({ ...draft, activityCommands: value })
            }
          />
        ) : (
          <div className="tool-list" style={{ maxHeight: 420 }}>
            {commands.map(([id, label, key]) => (
              <div className="form-row" style={{ padding: 8 }} key={id}>
                <span>{label}</span>
                <div style={{ display: "flex", gap: 8 }}>
                  {[0, 1].map((slot) => (
                    <input
                      key={slot}
                      aria-label={`${label} phím ${slot + 1}`}
                      value={(draft.shortcuts[id] ?? [key, ""])[slot] ?? ""}
                      readOnly
                      onKeyDown={(e) => {
                        if (e.key === "Tab") return;
                        e.preventDefault();
                        if (["Control", "Shift", "Alt", "Meta"].includes(e.key))
                          return;
                        const values = [...(draft.shortcuts[id] ?? [key, ""])];
                        values[slot] = ["Backspace", "Delete"].includes(e.key)
                          ? ""
                          : [
                              e.ctrlKey ? "Ctrl" : "",
                              e.shiftKey ? "Shift" : "",
                              e.altKey ? "Alt" : "",
                              e.key === " "
                                ? "Space"
                                : e.key.length === 1
                                  ? e.key.toUpperCase()
                                  : e.key,
                            ]
                              .filter(Boolean)
                              .join("+");
                        setDraft({
                          ...draft,
                          shortcuts: { ...draft.shortcuts, [id]: values },
                        });
                      }}
                    />
                  ))}
                </div>
              </div>
            ))}
          </div>
        )}
        <p className="form-error">{task.error}</p>
      </div>
      <div className="modal-footer">
        <Button onClick={() => setDraft({ ...draft, shortcuts: {} })}>
          Khôi phục phím mặc định
        </Button>
        <Button onClick={onClose}>Hủy</Button>
        <Button
          variant="primary"
          disabled={task.busy}
          onClick={() =>
            void task.run(async () => {
              validate();
              await onSave(draft);
            })
          }
        >
          Lưu cài đặt
        </Button>
      </div>
    </Modal>
  );
}
function ActivitySettings({
  commands,
  value,
  onChange,
}: {
  commands: string[][];
  value: string[];
  onChange: (ids: string[]) => void;
}) {
  const ordered = [
    ...value,
    ...commands.map((c) => c[0]).filter((id) => !value.includes(id)),
  ];
  return (
    <div className="tool-list" style={{ maxHeight: 420 }}>
      {ordered.map((id) => {
        const command = commands.find((c) => c[0] === id);
        if (!command) return null;
        return (
          <div className="tool-toolbar" style={{ padding: 8 }} key={id}>
            <label className="inline-checks">
              <input
                type="checkbox"
                checked={value.includes(id)}
                onChange={(e) =>
                  onChange(
                    e.target.checked
                      ? [...value, id]
                      : value.filter((v) => v !== id),
                  )
                }
              />
              {command[1]}
            </label>
            <div className="toolbar-spacer" />
            {[-1, 1].map((step) => (
              <Button
                key={step}
                disabled={
                  !value.includes(id) ||
                  value.indexOf(id) + step < 0 ||
                  value.indexOf(id) + step >= value.length
                }
                onClick={() => {
                  const next = [...value],
                    from = next.indexOf(id),
                    to = from + step;
                  [next[from], next[to]] = [next[to], next[from]];
                  onChange(next);
                }}
              >
                {step < 0 ? "↑" : "↓"}
              </Button>
            ))}
          </div>
        );
      })}
    </div>
  );
}
