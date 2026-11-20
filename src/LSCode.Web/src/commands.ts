import type { Settings } from "./bridge";
export const commands = [
  ["db.toggle", "Chuyển Database App / SYS", "Ctrl+Alt+D"],
  ["file.new", "Tạo file SQL", "Ctrl+N"],
  ["file.open", "Mở file", "Ctrl+O"],
  ["window.new", "Mở cửa sổ mới", "Ctrl+Shift+N"],
  ["folder.open", "Mở thư mục", "Ctrl+Shift+O"],
  ["file.save", "Lưu file", "Ctrl+S"],
  ["file.saveAs", "Lưu thành", "Ctrl+Shift+S"],
  ["file.close", "Đóng tab", "Ctrl+W"],
  ["sql.run", "Chạy SQL", "F5"],
  ["sql.cancel", "Dừng SQL", ""],
  ["sql.complete", "Gợi ý SQL", "Ctrl+Space"],
  ["sql.refresh", "Tải lại gợi ý SQL", ""],
  ["sql.export", "Xuất kết quả CSV", ""],
  ["editor.split", "Chia editor", "Ctrl+\\"],
  ["editor.find", "Tìm trong file", "Ctrl+F"],
  ["editor.sections", "Đi đến phần trong file", "Ctrl+G"],
  ["request.command", "Mở ô lệnh QLYC", "Ctrl+Alt+C"],
  ["editor.replace", "Thay thế trong file", "Ctrl+H"],
  ["editor.format", "Định dạng SQL / XML / JSON", ""],
  ["workspace.files", "File dự án", ""],
  ["workspace.objects", "DB Objects", ""],
  ["workspace.search", "Tìm trong dự án", "Ctrl+Shift+F"],
  ["workspace.refresh", "Tải lại cây file", ""],
  ["project.open", "Chọn dự án", ""],
  ["project.metadata", "Xem metadata dự án", ""],
  ["connection", "Kết nối SQL", ""],
  ["table", "Cấu trúc và dữ liệu", "Ctrl+Shift+T"],
  ["advance-note", "Generate Update", "Ctrl+Shift+4"],
  ["catalog", "Tạo danh mục", ""],
  ["snippets", "Snippets", ""],
  ["compare", "So sánh văn bản / thư mục", ""],
  ["beautify", "Beautify SQL", ""],
  ["profiler", "SQL Profiler", ""],
  ["check-mail", "Check Mail · Kiểm tra gửi mail", ""],
  ["template-studio", "Thiết kế template API", ""],
  ["xml", "XML: Outline / XPath", ""],
  ["xml.include", "Xem nhanh include", "F12"],
  ["xml.include.open", "Mở file include", "Ctrl+F12"],
  ["translation", "Dịch văn bản", ""],
  ["external.crystal", "Mở Crystal report", ""],
  ["theme.switch", "Đổi theme", ""],
  ["settings", "Cài đặt & phím tắt", ""],
];

export const shortcutGroups = [
  { id: "files", label: "File & cửa sổ", prefixes: ["file.", "folder.", "window."] },
  { id: "editor", label: "Editor & XML", prefixes: ["editor.", "xml"] },
  { id: "sql", label: "SQL & dữ liệu", prefixes: ["sql.", "db.", "table", "connection", "catalog", "profiler"] },
  { id: "workspace", label: "Dự án & không gian làm việc", prefixes: ["workspace.", "project."] },
  { id: "tools", label: "Công cụ", prefixes: ["request.", "advance-note", "snippets", "compare", "beautify", "template-studio", "check-mail", "translation", "external."] },
  { id: "system", label: "Giao diện & hệ thống", prefixes: ["theme.", "settings"] },
] as const;

export function shortcutGroupFor(commandId: string) {
  return shortcutGroups.find(group => group.prefixes.some(prefix =>
    commandId === prefix || commandId.startsWith(prefix),
  ))?.id ?? "tools";
}

export function keyboardGesture(event: Pick<KeyboardEvent, "ctrlKey" | "shiftKey" | "altKey" | "key" | "code">) {
  const digit = /^Digit([0-9])$/.exec(event.code)?.[1];
  const key = digit ?? (event.key === " "
    ? "Space"
    : event.key.length === 1
      ? event.key.toUpperCase()
      : event.key);
  return [
    event.ctrlKey ? "Ctrl" : "",
    event.shiftKey ? "Shift" : "",
    event.altKey ? "Alt" : "",
    key,
  ].filter(Boolean).join("+");
}
export const legacyAliases: Record<string, string> = {
  "sql.new": "file.new",
  "workspace.open": "folder.open",
  "project.select": "project.open",
  "sql.tables": "table",
  "sql.connection": "connection",
  "sql.execute": "sql.run",
  "tools.translate": "translation",
  "editor.shortcuts": "settings",
  "snippet.open": "snippets",
  "tools.profiler": "profiler",
  "tools.compare-text": "compare",
  "tools.compare-folder": "compare",
  "tools.beautify-sql": "beautify",
  "compare.file": "compare",
  "project.profile": "project.open",
  "project.import": "project.open",
};
export function migratePreferences(settings: Settings): Settings {
  const shortcuts = { ...settings.shortcuts };
  for (const [old, current] of Object.entries(legacyAliases))
    if (shortcuts[old] && !shortcuts[current])
      shortcuts[current] = shortcuts[old];
  if (!shortcuts["file.new"])
    shortcuts["file.new"] = ["Ctrl+N", "Ctrl+Shift+Q"];
  return {
    ...settings,
    shortcuts,
    activityCommands: settings.activityCommands?.map(
      (id) => legacyAliases[id] ?? id,
    ),
  };
}
