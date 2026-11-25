import { useDeferredValue, useEffect, useMemo, useRef, useState } from "react";
import { ArrowLeft, ArrowRight, Download, FileText, FolderOpen, Plus, RefreshCw, Search, Trash2, Upload } from "lucide-react";
import { Button, IconButton, Modal, Select } from "../ui";
import { operation, request } from "../bridge";
import type { Project, Settings } from "../bridge";
import { useTask } from "./useTask";
import { LoadingLine } from "../LoadingLine";

type Mode = "list" | "create" | "edit" | "delete" | "sync-user" | "import";
type Profile = {
  code: string; title: string; version: string; active: boolean;
  winProgramPath: string; webProgramPath: string; winSourcePath: string; webSourcePath: string;
  webHost1: string; webHost2: string; updatePath: string; server: string;
  authentication: string; user: string; password: string; database: string; defaultUser: string;
  listAllData: string; sortOrder: string; homeUpdateCode: string; notes: string;
};
type SyncSummary = { added: number; updated: number; unchanged: number; skipped: number };
type ImportPreview = { total: number; added: number; updated: number; skipped: number; fileName: string; overwrite: boolean };
type ImportResult = { projects: Project[]; added: number; updated: number; skipped: number };
type ExportResult = { path: string; projectCount: number; byteCount: number };

const pageSize = 50;
const emptyProfile = (): Profile => ({
  code: "", title: "", version: "", active: true,
  winProgramPath: "", webProgramPath: "", winSourcePath: "", webSourcePath: "",
  webHost1: "", webHost2: "", updatePath: "", server: ".", authentication: "sql",
  user: "", password: "", database: "", defaultUser: "ABC", listAllData: "",
  sortOrder: "9999", homeUpdateCode: "", notes: "",
});

export function ProjectDialog({ projects, lastAccess, onProjects, onOpen, onClose }: {
  projects: Project[];
  lastAccess?: Record<string, string>;
  onProjects: (projects: Project[]) => void;
  onOpen: (project: Project) => Promise<void>;
  onClose: () => void;
}) {
  const [mode, setMode] = useState<Mode>("list");
  const [search, setSearch] = useState("");
  const deferredSearch = useDeferredValue(search.trim().toLocaleLowerCase("vi"));
  const [page, setPage] = useState(1);
  const [profile, setProfile] = useState<Profile>(emptyProfile);
  const [editing, setEditing] = useState<Project>();
  const [deleting, setDeleting] = useState<Project>();
  const [metadata, setMetadata] = useState("");
  const [importPath, setImportPath] = useState("");
  const [importPreview, setImportPreview] = useState<ImportPreview>();
  const [overwrite, setOverwrite] = useState(false);
  const [currentUser, setCurrentUser] = useState("");
  const [syncProject, setSyncProject] = useState<Project>();
  const [syncMessage, setSyncMessage] = useState("");
  const [sharedAccess, setSharedAccess] = useState<Record<string, string>>();
  const [opening, setOpening] = useState(false);
  const pending = useRef<{ cancel: () => Promise<unknown> }>();
  const task = useTask();

  useEffect(() => {
    let alive = true;
    void request<Settings>("settings.read").then(value => {
      if (!alive) return;
      setSharedAccess(value.projectLastAccess);
      setCurrentUser(value.standardCatalogUser?.trim() ?? "");
    }).catch(() => {});
    return () => { alive = false; void pending.current?.cancel().catch(() => {}); };
  }, []);

  const filtered = useMemo(() => {
    const access = (id: string) => Math.max(Date.parse(lastAccess?.[id] ?? "") || 0, Date.parse(sharedAccess?.[id] ?? "") || 0);
    return projects.filter(project => !deferredSearch || [project.code, project.title, project.programPath,
      project.server, project.database].join(" ").toLocaleLowerCase("vi").includes(deferredSearch))
      .sort((left, right) => access(right.id) - access(left.id) || left.code.localeCompare(right.code, "vi"));
  }, [projects, deferredSearch, lastAccess, sharedAccess]);
  const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize));
  const currentPage = Math.min(page, totalPages);
  const visible = useMemo(() => filtered.slice((currentPage - 1) * pageSize, currentPage * pageSize), [filtered, currentPage]);
  useEffect(() => setPage(1), [deferredSearch]);

  const back = () => { setMode("list"); setImportPreview(undefined); setImportPath(""); task.clearError(); };
  const update = <K extends keyof Profile>(key: K, value: Profile[K]) => setProfile(old => ({ ...old, [key]: value }));
  async function open(project: Project) {
    setOpening(true);
    try { await onOpen(project); } finally { setOpening(false); }
  }
  async function edit(project: Project) {
    await task.run(async () => {
      const detail = await request<{ profile: Omit<Profile, "password">; metadata: string }>("project.details", { id: project.id });
      setEditing(project);
      setProfile({ ...emptyProfile(), ...detail.profile, password: "" });
      setMetadata(detail.metadata);
      setMode("edit");
    });
  }
  async function syncStandard(user: string, project?: Project, useFsdFallback = false) {
    await task.run(async () => {
      const name = user.trim() || await request<string>("project.standard.user");
      if (!name) { setMode("sync-user"); return; }
      setCurrentUser(name);
      const current = operation<{ currentUser: string; summary: SyncSummary; projects: Project[] }>("project.standard.sync",
        { currentUser: name, projectId: project?.id, useFsdFallback });
      pending.current = current;
      const result = await current.promise.finally(() => { pending.current = undefined; });
      onProjects(result.projects);
      setCurrentUser(result.currentUser);
      const { added, updated, unchanged, skipped } = result.summary;
      setSyncMessage(project
        ? updated ? `Đã cập nhật ${project.code}.` : `${project.code} đã có thông tin mới nhất.`
        : added + updated + unchanged + skipped === 0
          ? `Danh mục chuẩn không trả về dự án cho ${result.currentUser}. Bấm vào tên người dùng để kiểm tra hoặc đổi CurrUser.`
          : `Đã thêm ${added} dự án, cập nhật ${updated}, giữ nguyên ${unchanged}, bỏ qua ${skipped} mục không hợp lệ.`);
      setMode("list");
    });
  }
  function startSync(project?: Project) {
    setSyncProject(project);
    setSyncMessage("");
    void syncStandard(currentUser, project, !project);
  }
  async function chooseImport() {
    await task.run(async () => {
      const path = await request<string | null>("project.import.choose");
      if (!path) return;
      const current = operation<Omit<ImportPreview, "overwrite">>("project.import.previewFile", { path, overwrite }, 180_000);
      pending.current = current;
      const result = await current.promise;
      setImportPath(path);
      setImportPreview({ ...result, overwrite });
      setMode("import");
    });
  }
  async function refreshImportPreview(nextOverwrite = overwrite) {
    if (!importPath) return;
    await task.run(async () => {
      const current = operation<Omit<ImportPreview, "overwrite">>("project.import.previewFile",
        { path: importPath, overwrite: nextOverwrite }, 180_000);
      pending.current = current;
      setImportPreview({ ...await current.promise, overwrite: nextOverwrite });
    });
  }
  async function applyImport() {
    if (!importPath || importPreview?.overwrite !== overwrite) return;
    await task.run(async () => {
      const current = operation<ImportResult>("project.import.applyFile", { path: importPath, overwrite }, 180_000);
      pending.current = current;
      const result = await current.promise;
      onProjects(result.projects);
      setSyncMessage(`Đã import ${result.added} dự án mới, ${result.updated} cập nhật, ${result.skipped} bỏ qua.`);
      back();
    });
  }
  async function exportProjects() {
    await task.run(async () => {
      const path = await request<string | null>("project.export.choose", { name: "projects.xml" });
      if (!path) return;
      const current = operation<ExportResult>("project.export.file", { path }, 180_000);
      pending.current = current;
      const result = await current.promise;
      setSyncMessage(`Đã xuất ${result.projectCount.toLocaleString("vi-VN")} dự án ra ${result.path}.`);
    });
  }

  const title = mode === "list" ? "Dự án của bạn" : mode === "create" ? "Tạo profile dự án" :
    mode === "edit" ? `Sửa profile · ${profile.code}` : mode === "sync-user" ? "Nhập tên" :
    mode === "import" ? "Import catalog XML" : "Xóa profile";

  return <Modal title={title} onClose={onClose} wide className="project-catalog-modal">
    <div className="project-dialog-body">
      {mode !== "list" && <Button className="project-back" variant="ghost" onClick={back}><ArrowLeft size={15} /> Danh sách dự án</Button>}
      {mode === "list" && <>
        <div className="project-actions" aria-label="Thao tác dự án">
          <Button variant="primary" disabled={task.busy} onClick={() => startSync()}><RefreshCw size={15} /> Đồng bộ</Button>
          {task.busy && pending.current && <Button onClick={() => void pending.current?.cancel()}>Dừng</Button>}
          <Button disabled={task.busy} onClick={() => { setEditing(undefined); setProfile(emptyProfile()); setMode("create"); }}><Plus size={15} /> Tạo profile</Button>
          <Button disabled={task.busy} onClick={() => void chooseImport()}><Upload size={15} /> Import XML</Button>
          <Button disabled={task.busy} onClick={() => void exportProjects()}><Download size={15} /> Export XML</Button>
          {currentUser && <Button variant="ghost" className="project-sync-user" disabled={task.busy} onClick={() => { setSyncProject(undefined); setMode("sync-user"); }}>{currentUser}</Button>}
        </div>
        <LoadingLine busy={task.busy || opening} label={opening ? "Đang mở dự án…" : "Đang đồng bộ danh mục…"} />
        {syncMessage && <p className="project-sync-result" role="status">{syncMessage}</p>}
        <div className="project-search-row">
          <label className="project-search"><Search size={16} /><input aria-label="Tìm dự án" placeholder="Tìm mã, tên, đường dẫn, server hoặc database"
            value={search} onChange={event => setSearch(event.target.value)} /></label>
          <span>{filtered.length.toLocaleString("vi-VN")} dự án</span>
        </div>
        <div className="project-list project-catalog" aria-busy={opening}>
          {visible.map(project => <div className="project-list-entry" key={project.id}>
            <button className="project-row" disabled={task.busy} title="Nhấp để mở · Chuột phải để sửa"
              onContextMenu={event => { event.preventDefault(); void edit(project); }} onClick={() => void task.run(() => open(project))}>
              <FolderOpen size={18} className="project-icon" />
              <span className="project-row-content">
                <span className="project-row-heading"><b title={project.code}>{project.code}</b><strong title={project.title || project.code}>{project.title || project.code}</strong>{!project.active && <em>Ngừng dùng</em>}</span>
                <small title={project.programPath}>{project.programPath || "Chưa khai báo đường dẫn chương trình"}</small>
              </span><ArrowRight size={16} />
            </button>
            <IconButton label={`Đồng bộ lại ${project.code}`} disabled={task.busy} onClick={() => startSync(project)}><RefreshCw size={15} /></IconButton>
            <IconButton destructive label={`Xóa profile ${project.code}`} disabled={task.busy} onClick={() => { setDeleting(project); setMode("delete"); }}><Trash2 size={15} /></IconButton>
          </div>)}
          {filtered.length === 0 && <div className="empty">{projects.length ? "Không tìm thấy dự án phù hợp." : "Chưa có dự án. Hãy đồng bộ hoặc tạo profile."}</div>}
        </div>
        {filtered.length > pageSize && <div className="project-pagination"><Button disabled={currentPage <= 1} onClick={() => setPage(value => value - 1)}>Trước</Button>
          <span>Trang {currentPage} / {totalPages}</span><Button disabled={currentPage >= totalPages} onClick={() => setPage(value => value + 1)}>Sau</Button></div>}
      </>}

      {mode === "sync-user" && <form className="project-compact-form" onSubmit={event => { event.preventDefault(); void syncStandard(currentUser, syncProject); }}>
        <label>Người sử dụng<input autoFocus required value={currentUser} onChange={event => setCurrentUser(event.target.value)} /></label>
        <div className="form-actions"><Button variant="primary" disabled={task.busy || !currentUser.trim()}>Đồng bộ</Button>
          {task.busy && <Button type="button" onClick={() => void pending.current?.cancel()}>Dừng</Button>}</div>
      </form>}

      {(mode === "create" || mode === "edit") && <ProjectProfileForm profile={profile} update={update} busy={task.busy}
        editing={mode === "edit"} metadata={metadata} onChooseFolder={() => void task.run(async () => {
          const path = await request<string | null>("workspace.folder"); if (path) update("webSourcePath", path);
        })} onSubmit={() => void task.run(async () => {
          const updated = await request<Project>(mode === "edit" ? "project.update" : "project.create",
            { ...profile, id: editing?.id, sourcePath: profile.webSourcePath || profile.webProgramPath || profile.winSourcePath || profile.winProgramPath });
          onProjects(await request<Project[]>("project.list")); setMode("list"); if (mode === "edit") await open(updated);
        })} />}

      {mode === "delete" && deleting && <div className="project-confirm"><p>Xóa profile <strong>{deleting.code}</strong> khỏi danh sách? Thư mục và database vẫn được giữ.</p>
        <div className="form-actions"><Button onClick={back}>Hủy</Button><Button variant="destructive" disabled={task.busy}
          onClick={() => void task.run(async () => { await request("project.delete", { id: deleting.id }); onProjects(await request<Project[]>("project.list")); setDeleting(undefined); setMode("list"); })}>Xóa profile</Button></div></div>}

      {mode === "import" && importPreview && <div className="project-import-summary">
        <div className="project-import-file"><FileText size={18} /><span><strong>{importPreview.fileName}</strong><small title={importPath}>{importPath}</small></span></div>
        <dl><div><dt>Tổng số</dt><dd>{importPreview.total.toLocaleString("vi-VN")}</dd></div>
          <div><dt>Dự án mới</dt><dd>{importPreview.added.toLocaleString("vi-VN")}</dd></div>
          <div><dt>Cập nhật</dt><dd>{importPreview.updated.toLocaleString("vi-VN")}</dd></div>
          <div><dt>Bỏ qua</dt><dd>{importPreview.skipped.toLocaleString("vi-VN")}</dd></div></dl>
        <label className="checkbox"><input type="checkbox" checked={overwrite} onChange={event => setOverwrite(event.target.checked)} /> Cập nhật dự án trùng</label>
        {importPreview.overwrite !== overwrite && <p className="project-import-notice">Tùy chọn đã thay đổi. Hãy cập nhật bản xem trước trước khi import.</p>}
        <div className="form-actions"><Button disabled={task.busy} onClick={() => void chooseImport()}>Chọn file khác</Button>
          {importPreview.overwrite !== overwrite && <Button disabled={task.busy} onClick={() => void refreshImportPreview()}>Cập nhật xem trước</Button>}
          <Button variant="primary" disabled={task.busy || importPreview.overwrite !== overwrite} onClick={() => void applyImport()}>Import catalog</Button></div>
      </div>}
      <p className="form-error" role="alert">{task.error}</p>
    </div>
  </Modal>;
}

function ProjectProfileForm({ profile, update, busy, editing, metadata, onChooseFolder, onSubmit }: {
  profile: Profile; update: <K extends keyof Profile>(key: K, value: Profile[K]) => void;
  busy: boolean; editing: boolean; metadata: string; onChooseFolder: () => void; onSubmit: () => void;
}) {
  const field = (key: keyof Profile, label: string, type = "text", required = false) => <label>{label}<input type={type}
    required={required} value={String(profile[key])} onChange={event => update(key, event.target.value as never)} /></label>;
  return <form className="project-profile-form" onSubmit={event => { event.preventDefault(); onSubmit(); }}>
    <fieldset><legend>Nhận diện</legend><div className="profile-grid">{field("code", "Mã dự án", "text", true)}{field("version", "Phiên bản")}
      {field("title", "Tên dự án", "text", true)}<label className="checkbox"><input type="checkbox" checked={profile.active}
        onChange={event => update("active", event.target.checked)} /> Còn sử dụng</label></div></fieldset>
    <fieldset><legend>Đường dẫn</legend><div className="profile-grid">{field("winProgramPath", "Đường dẫn chương trình WinForms")}
      {field("webProgramPath", "Đường dẫn chương trình web")}{field("winSourcePath", "Source WinForms")}{field("webSourcePath", "Source web")}
      {field("webHost1", "Web Host 1")}{field("webHost2", "Web Host 2")}{field("updatePath", "Đường dẫn update")}
      {field("homeUpdateCode", "Mã dự án update ở nhà")}</div><Button type="button" onClick={onChooseFolder}>Chọn thư mục source web</Button></fieldset>
    <fieldset><legend>Kết nối</legend><div className="profile-grid">{field("server", "Server")}<label>Loại kết nối
      <Select label="Loại kết nối" value={profile.authentication || "sql"} onChange={value => update("authentication", value)}
        options={[{ value: "sql", label: "SQL Server Authentication" }, { value: "windows", label: "Windows Authentication" }]} /></label>
      {field("user", "Tên đăng nhập")}{field("password", editing ? "Mật khẩu mới (để trống để giữ nguyên)" : "Mật khẩu", "password")}
      {field("database", "Database SYS")}{field("defaultUser", "User mặc định")}</div></fieldset>
    <fieldset><legend>Thiết lập</legend><div className="profile-grid">{field("listAllData", "List All Data (1 - All)")}
      {field("sortOrder", "Số thứ tự", "number")}<label className="profile-span">Ghi chú<textarea value={profile.notes}
        onChange={event => update("notes", event.target.value)} /></label></div></fieldset>
    {editing && <details><summary>Metadata dự án</summary><pre className="metadata-preview">{metadata}</pre></details>}
    <div className="form-actions"><Button variant="primary" disabled={busy}>{editing ? "Lưu thay đổi" : "Tạo profile"}</Button></div>
  </form>;
}
