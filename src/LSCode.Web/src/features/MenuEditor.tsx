import { useEffect, useMemo, useRef, useState, type FormEvent } from "react";
import { FileCode2, FolderSearch, Plus, RefreshCw, Save } from "lucide-react";
import { request, type Target } from "../bridge";
import { Button, Modal } from "../ui";
import { FileActionMenu, type FileMenuItem, type FileMenuState } from "../FileActionMenu";
import "./menuEditor.css";

export type MenuCommand = {
  entry: { id: string; caption: string; sysId: string; commandId: string; link: string };
};
type Field = { name: string; type: string; maxLength: number; nullable: boolean; readOnly: boolean };
type ResourceFile = { path: string; tables: string[] };
type Resource = {
  kind: string;
  status: "found" | "missing" | "multiple";
  paths: string[];
  files?: ResourceFile[];
};
type Model = {
  mode: "edit" | "new";
  fields: Field[];
  values: Record<string, string | null>;
  version: string;
  canSave: boolean;
};
type ResourceResult = {
  controllers: string[];
  resources: Resource[];
  tables: string[];
};
type ResourceKind = "Dir" | "Filter" | "Grid" | "Report" | "Lookup" | "Upload";

const resourceKinds: ResourceKind[] = ["Dir", "Filter", "Grid", "Report", "Lookup", "Upload"];
const labels: Record<string, string> = {
  wmenu_id: "Mã cây", wmenu_id0: "Mã cha", bar: "Tên tiếng Việt", bar2: "Tên tiếng Anh",
  menu_id: "Mã command", link: "Đường dẫn Main", parameter: "Tham số", icon_url: "Icon URL",
  status: "Trạng thái", icon: "Icon", sysid: "Controller / sysid", type: "Loại", syscode: "Syscode",
  msys: "Msys", target: "Target", xtype: "Xtype", edition: "Edition", expi_icon: "Expi icon",
};
const primaryFields = new Set(["wmenu_id", "wmenu_id0", "bar", "bar2", "menu_id", "link", "sysid"]);

export function MenuEditor({ target, command, root, onClose, onSaved, onOpenPath, onCopy, onReveal, onDuplicate, onRename }: {
  target: Target;
  command: MenuCommand;
  root: string;
  onClose: () => void;
  onSaved: (id: string) => Promise<void> | void;
  onOpenPath: (path: string) => void;
  onCopy: (text: string) => void;
  onReveal: (path: string) => void;
  onDuplicate?: (path: string) => void | Promise<unknown>;
  onRename?: (path: string, name: string) => Promise<unknown>;
}) {
  const [model, setModel] = useState<Model>();
  const [values, setValues] = useState<Record<string, string | null>>({});
  const [resources, setResources] = useState<ResourceResult>();
  const [resourceTab, setResourceTab] = useState<ResourceKind>("Dir");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [recordId, setRecordId] = useState(command.entry.id);
  const [fileMenu, setFileMenu] = useState<FileMenuState>();
  const [renaming, setRenaming] = useState<FileMenuItem>();
  const [newName, setNewName] = useState("");
  const [renameBusy, setRenameBusy] = useState(false);
  const [renameError, setRenameError] = useState("");
  const menuOwner = useRef<HTMLButtonElement>();
  const renameInput = useRef<HTMLInputElement>(null);
  const dirty = useMemo(() => !!model && JSON.stringify(values) !== JSON.stringify(model.values), [model, values]);

  async function load() {
    setFileMenu(undefined);
    setBusy(true); setError(""); setNotice("");
    try {
      const [loaded, linked] = await Promise.all([
        request<Model>("project.menu.details", { targetId: target.id, wmenuId: command.entry.id }),
        request<ResourceResult>("project.menu.resources", { root, link: command.entry.link, sysId: command.entry.sysId }),
      ]);
      setModel(loaded); setValues(loaded.values); setResources(linked); setResourceTab("Dir"); setRecordId(command.entry.id);
    } catch (e) { setError(e instanceof Error ? e.message : String(e)); }
    finally { setBusy(false); }
  }
  useEffect(() => { void load(); }, [target.id, command.entry.id]);
  useEffect(() => {
    if (renaming) renameInput.current?.focus();
  }, [renaming]);

  async function createNew() {
    setBusy(true); setError(""); setNotice("");
    try {
      const loaded = await request<Model>("project.menu.new", { targetId: target.id, selectedId: command.entry.id });
      setModel(loaded); setValues(loaded.values); setRecordId("");
    } catch (e) { setError(e instanceof Error ? e.message : String(e)); }
    finally { setBusy(false); }
  }
  async function save() {
    if (!model) return;
    setBusy(true); setError(""); setNotice("");
    try {
      const loaded = await request<Model>("project.menu.save", {
        targetId: target.id, mode: model.mode, originalId: recordId, version: model.version, values,
      });
      setModel(loaded); setValues(loaded.values); setRecordId(loaded.values.wmenu_id ?? recordId);
      setNotice("Đã lưu menu. Cây chức năng đã được tải lại.");
      await onSaved(loaded.values.wmenu_id ?? command.entry.id);
    } catch (e) { setError(e instanceof Error ? e.message : String(e)); }
    finally { setBusy(false); }
  }
  const renderField = (field: Field) => {
    const limit = field.maxLength > 0 ? (field.type.startsWith("n") ? field.maxLength / 2 : field.maxLength) : undefined;
    return <label key={field.name}>
      <span>{labels[field.name] ?? field.name} <small>{field.type}{limit ? `(${limit})` : ""}</small></span>
      <input
        value={values[field.name] ?? ""}
        readOnly={field.readOnly || (model?.mode === "edit" && field.name === "wmenu_id")}
        maxLength={limit}
        onChange={(event) => setValues((old) => ({ ...old, [field.name]: event.target.value }))}
      />
    </label>;
  };
  const activeResource = resources?.resources.find((resource) => resource.kind === resourceTab);
  const resourceFiles = activeResource?.files ?? activeResource?.paths.map((path) => ({ path, tables: [] })) ?? [];
  const resolveResourcePath = (path: string) => root + (root.endsWith("\\") || root.endsWith("/") ? "" : "\\") + path;
  const resourceItem = (file: ResourceFile): FileMenuItem => ({
    name: file.path.split(/[\\/]/).at(-1) ?? file.path,
    path: resolveResourcePath(file.path),
    directory: false,
  });
  function beginRename(item: FileMenuItem) {
    setFileMenu(undefined);
    setRenaming(item);
    setNewName(item.name);
    setRenameError("");
  }
  async function submitRename(event: FormEvent) {
    event.preventDefault();
    if (!renaming || !onRename || renameBusy) return;
    setRenameBusy(true); setRenameError("");
    try {
      await onRename(renaming.path, newName);
      setRenaming(undefined);
      await load();
    } catch (e) {
      setRenameError(e instanceof Error ? e.message : String(e));
    } finally {
      setRenameBusy(false);
    }
  }

  return <div className="menu-editor-shell"><Modal wide title={model?.mode === "new" ? "Thêm menu ERP" : "Thông tin menu ERP"} onClose={onClose}>
    <div className="menu-editor-toolbar">
      <div><strong>{command.entry.caption}</strong><span>{target.database} · {target.server}</span></div>
      <Button onClick={() => void load()} disabled={busy}><RefreshCw size={15} />Tải lại</Button>
      <Button onClick={() => void createNew()} disabled={busy || dirty}><Plus size={15} />Thêm mới</Button>
      <Button variant="primary" onClick={() => void save()} disabled={busy || !model?.canSave || !dirty}><Save size={15} />Lưu</Button>
    </div>
    {(error || notice) && <div role="status" className={`menu-editor-message ${error ? "error" : "success"}`}>{error || notice}</div>}
    <div className="menu-editor-body" aria-busy={busy}>
      <section className="menu-editor-form">
        <h3>Thông tin từ dbo.wcommand</h3>
        {!model ? <p className="menu-editor-muted">{busy ? "Đang tải thông tin…" : "Không có dữ liệu."}</p> : <>
          <div className="menu-editor-fields">{model.fields.filter((field) => primaryFields.has(field.name)).map(renderField)}</div>
          <details open><summary>Thuộc tính kỹ thuật</summary><div className="menu-editor-fields technical">
            {model.fields.filter((field) => !primaryFields.has(field.name)).map(renderField)}
          </div></details>
        </>}
      </section>
      <section className="menu-editor-resources" aria-label="File liên quan">
        <div className="menu-editor-resource-tabs" role="tablist" aria-label="Nhóm file">
          {resourceKinds.map((kind) => {
            const resource = resources?.resources.find((item) => item.kind === kind);
            const count = resource?.files?.length ?? resource?.paths.length ?? 0;
            return <button
              key={kind}
              type="button"
              role="tab"
              aria-selected={resourceTab === kind}
              onClick={() => setResourceTab(kind)}
            >{kind} <span>({count})</span></button>;
          })}
        </div>
        <div className="menu-editor-resource-grid" role="tabpanel">
          <div className="menu-editor-resource-head"><span>File</span><span>Table</span></div>
          {!resources ? (
            <p className="menu-editor-muted"><FolderSearch size={18} />{busy ? "Đang dò file liên quan…" : "Chưa có kết quả."}</p>
          ) : resourceFiles.length ? resourceFiles.map((file) => (
            <button
              className="menu-editor-resource-row"
              type="button"
              key={file.path}
              title={resolveResourcePath(file.path)}
              onClick={() => onOpenPath(resolveResourcePath(file.path))}
              onContextMenu={(event) => {
                event.preventDefault();
                event.currentTarget.focus();
                const item = resourceItem(file);
                menuOwner.current = event.currentTarget;
                setFileMenu({ item, x: Math.max(8, Math.min(event.clientX, innerWidth - 248)), y: Math.max(8, Math.min(event.clientY, innerHeight - 220)) });
              }}
              onKeyDown={(event) => {
                const item = resourceItem(file);
                if (event.key === "F2" && onRename) {
                  event.preventDefault();
                  event.stopPropagation();
                  beginRename(item);
                } else if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "c") {
                  event.preventDefault();
                  event.stopPropagation();
                  onCopy(item.name);
                }
              }}
            >
              <span><FileCode2 size={15} />{file.path}</span>
              <span>{file.tables.join(", ") || "—"}</span>
            </button>
          )) : <p className="menu-editor-muted">Không có file trong nhóm {resourceTab}.</p>}
        </div>
      </section>
    </div>
    <div className="modal-footer"><span className="menu-editor-dirty">{dirty ? "Có thay đổi chưa lưu" : "Không có thay đổi"}</span><Button onClick={onClose}>Đóng</Button><Button variant="primary" onClick={() => void save()} disabled={busy || !dirty}><Save size={15} />Lưu</Button></div>
    <FileActionMenu
      menu={fileMenu}
      onClose={() => setFileMenu(undefined)}
      onCopy={onCopy}
      onReveal={onReveal}
      onDuplicate={onDuplicate ? (path) => { onClose(); void onDuplicate(path); } : undefined}
      onRenameRequest={onRename ? beginRename : undefined}
      restoreFocus={() => menuOwner.current?.focus()}
      inline
    />
  </Modal>
  {renaming && onRename && <Modal title="Đổi tên file" onClose={() => { if (!renameBusy) setRenaming(undefined); }}>
    <form className="tool-body" onSubmit={submitRename}>
      <label>Tên mới<input ref={renameInput} aria-label="Tên mới" value={newName} disabled={renameBusy}
        onChange={(event) => setNewName(event.target.value)}
        onFocus={(event) => {
          const extension = newName.lastIndexOf(".");
          event.currentTarget.setSelectionRange(0, extension > 0 ? extension : newName.length);
        }} /></label>
      {renameError && <p role="alert" className="form-error">{renameError}</p>}
      <div className="inline-checks">
        <Button type="button" disabled={renameBusy} onClick={() => setRenaming(undefined)}>Hủy</Button>
        <Button type="submit" variant="primary" disabled={renameBusy || !newName.trim()}>{renameBusy ? "Đang đổi tên…" : "Đổi tên"}</Button>
      </div>
    </form>
  </Modal>}
  </div>;
}
