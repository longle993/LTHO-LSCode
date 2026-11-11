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
  ["editor.replace", "Thay thế trong file", "Ctrl+H"],
  ["editor.format", "Định dạng SQL / XML / JSON", ""],
  ["workspace.files", "File dự án", ""],
  ["workspace.objects", "DB Objects", ""],
  ["workspace.search", "Tìm trong dự án", "Ctrl+Shift+F"],
  ["workspace.refresh", "Tải lại cây file", ""],
  ["project.open", "Chọn dự án", ""],
  ["sync", "Đồng bộ dự án", ""],
  ["project.metadata", "Xem metadata dự án", ""],
  ["connection", "Kết nối SQL", ""],
  ["table", "Cấu trúc và dữ liệu", "Ctrl+Shift+T"],
  ["catalog", "Danh mục", ""],
  ["snippets", "Snippets", ""],
  ["compare", "So sánh văn bản / thư mục", ""],
  ["beautify", "Beautify SQL", ""],
  ["profiler", "SQL Profiler", ""],
  ["xml", "XML: Outline / XPath", ""],
  ["xml.include", "Xem nhanh include", "F12"],
  ["xml.include.open", "Mở file include", "Ctrl+F12"],
  ["translation", "Dịch văn bản", ""],
  ["external.crystal", "Mở Crystal report", ""],
  ["theme.switch", "Đổi theme", ""],
  ["settings", "Cài đặt & phím tắt", ""],
];
export const legacyAliases: Record<string, string> = {
  "sql.new": "file.new",
  "workspace.open": "folder.open",
  "project.select": "project.open",
  "project.sync": "sync",
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
