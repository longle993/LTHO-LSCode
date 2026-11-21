import { useMemo, useState } from "react";
import { Search } from "lucide-react";
import { Button, Modal, Select } from "../ui";
import { request } from "../bridge";
import type { Settings } from "../bridge";
import { keyboardGesture, shortcutGroupFor, shortcutGroups } from "../commands";
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
    [section, setSection] = useState("general"),
    [shortcutQuery, setShortcutQuery] = useState("");
  const task = useTask();
  const groupedShortcuts = useMemo(() => {
    const query = shortcutQuery.trim().toLocaleLowerCase();
    return shortcutGroups.map(group => ({
      ...group,
      commands: commands.filter(([id, label, key]) =>
        shortcutGroupFor(id) === group.id &&
        (!query || `${label} ${id} ${key}`.toLocaleLowerCase().includes(query))),
    })).filter(group => group.commands.length);
  }, [commands, shortcutQuery]);
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
        <div className="settings-tabs" role="tablist" aria-label="Nhóm cài đặt">
          <Button
            role="tab"
            aria-selected={section === "general"}
            variant={section === "general" ? "secondary" : "ghost"}
            onClick={() => setSection("general")}
          >
            Giao diện
          </Button>
          <Button
            role="tab"
            aria-selected={section === "activity"}
            variant={section === "activity" ? "secondary" : "ghost"}
            onClick={() => setSection("activity")}
          >
            Lối tắt bên trái
          </Button>
          <Button
            role="tab"
            aria-selected={section === "shortcuts"}
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
              SQL Profiler (PROFILER.EXE)
              <input
                value={draft.profiler18Path}
                onChange={(e) =>
                  setDraft({ ...draft, profiler18Path: e.target.value })
                }
              />
            </label>
            <Button type="button" disabled={task.busy} onClick={() => void task.run(async () => {
              const path = await request<string | null>("file.pick");
              if (path) setDraft(old => ({ ...old, profiler18Path: path }));
            })}>Chọn PROFILER.EXE</Button>
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
          <div className="shortcut-settings">
            <label className="shortcut-search">
              <Search size={16} aria-hidden="true" />
              <input type="search" aria-label="Tìm phím tắt" placeholder="Tìm lệnh hoặc phím tắt…"
                value={shortcutQuery} onChange={event => setShortcutQuery(event.target.value)} />
            </label>
            <div className="shortcut-groups">
              {groupedShortcuts.map(group => <section className="shortcut-group" key={group.id}>
                <h3>{group.label}</h3>
                {group.commands.map(([id, label, key]) => (
                  <div className="shortcut-row" key={id}>
                    <div><span>{label}</span><small>{id}</small></div>
                    <div className="shortcut-inputs">
                      {[0, 1].map((slot) => (
                        <input
                          key={slot}
                          aria-label={`${label} phím ${slot + 1}`}
                          value={(draft.shortcuts[id] ?? [key, ""])[slot] ?? ""}
                          placeholder={slot === 0 ? "Chưa đặt" : "Phím phụ"}
                          readOnly
                          onKeyDown={(e) => {
                            if (e.key === "Tab") return;
                            e.preventDefault();
                            if (["Control", "Shift", "Alt", "Meta"].includes(e.key)) return;
                            const values = [...(draft.shortcuts[id] ?? [key, ""])];
                            values[slot] = ["Backspace", "Delete"].includes(e.key) ? "" : keyboardGesture(e.nativeEvent);
                            setDraft({ ...draft, shortcuts: { ...draft.shortcuts, [id]: values } });
                          }}
                        />
                      ))}
                    </div>
                  </div>
                ))}
              </section>)}
              {!groupedShortcuts.length && <p className="empty">Không tìm thấy phím tắt phù hợp.</p>}
            </div>
          </div>
        )}
        <p className="form-error">{task.error}</p>
      </div>
      <div className="modal-footer">
        {section === "shortcuts" && <Button onClick={() => setDraft({ ...draft, shortcuts: {} })}>
          Khôi phục phím mặc định
        </Button>}
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
