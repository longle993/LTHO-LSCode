import { useEffect, useMemo, useState } from "react";
import { FileCode2, FolderSearch, Plus, RefreshCw, Save } from "lucide-react";
import { request, type Target } from "../bridge";
import { Button, Modal } from "../ui";
import "./menuEditor.css";

export type MenuCommand = {
  entry: { id: string; caption: string; sysId: string; commandId: string; link: string };
};
type Field = { name: string; type: string; maxLength: number; nullable: boolean; readOnly: boolean };
type Model = {
  mode: "edit" | "new";
  fields: Field[];
  values: Record<string, string | null>;
  version: string;
  canSave: boolean;
};
type ResourceResult = {
  controllers: string[];
  resources: { kind: string; status: "found" | "missing" | "multiple"; paths: string[] }[];
  tables: string[];
};

const labels: Record<string, string> = {
  wmenu_id: "Mã cây", wmenu_id0: "Mã cha", bar: "Tên tiếng Việt", bar2: "Tên tiếng Anh",
  menu_id: "Mã command", link: "Đường dẫn Main", parameter: "Tham số", icon_url: "Icon URL",
  status: "Trạng thái", icon: "Icon", sysid: "Controller / sysid", type: "Loại", syscode: "Syscode",
  msys: "Msys", target: "Target", xtype: "Xtype", edition: "Edition", expi_icon: "Expi icon",
};
const primaryFields = new Set(["wmenu_id", "wmenu_id0", "bar", "bar2", "menu_id", "link", "sysid"]);

export function MenuEditor({ target, command, root, onClose, onSaved, onOpenPath }: {
  target: Target;
  command: MenuCommand;
  root: string;
  onClose: () => void;
  onSaved: (id: string) => Promise<void> | void;
  onOpenPath: (path: string) => void;
}) {
  const [model, setModel] = useState<Model>();
  const [values, setValues] = useState<Record<string, string | null>>({});
  const [resources, setResources] = useState<ResourceResult>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [recordId, setRecordId] = useState(command.entry.id);
  const dirty = useMemo(() => !!model && JSON.stringify(values) !== JSON.stringify(model.values), [model, values]);

  async function load() {
    setBusy(true); setError(""); setNotice("");
    try {
      const [loaded, linked] = await Promise.all([
        request<Model>("project.menu.details", { targetId: target.id, wmenuId: command.entry.id }),
        request<ResourceResult>("project.menu.resources", { root, link: command.entry.link }),
      ]);
      setModel(loaded); setValues(loaded.values); setResources(linked); setRecordId(command.entry.id);
    } catch (e) { setError(e instanceof Error ? e.message : String(e)); }
    finally { setBusy(false); }
  }
  useEffect(() => { void load(); }, [target.id, command.entry.id]);

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

  return <div className="menu-editor-shell"><Modal title={model?.mode === "new" ? "Thêm menu ERP" : "Thông tin menu ERP"} onClose={onClose}>
    <div className="menu-editor-toolbar">
      <div><strong>{command.entry.caption}</strong><span>{target.database} · {target.server}</span></div>
      <Button onClick={() => void load()} disabled={busy}><RefreshCw size={15} />Tải lại</Button>
      <Button onClick={() => void createNew()} disabled={busy || dirty}><Plus size={15} />Thêm mới</Button>
      <Button variant="primary" onClick={() => void save()} disabled={busy || !model?.canSave || !dirty}><Save size={15} />Lưu</Button>
    </div>
    {(error || notice) && <div role="status" className={`menu-editor-message ${error ? "error" : "success"}`}>{error || notice}</div>}
    <div className="menu-editor-body" aria-busy={busy}>
      <section className="menu-editor-form">
        <h3>Thông tin menu</h3>
        {!model ? <p className="menu-editor-muted">{busy ? "Đang tải thông tin…" : "Không có dữ liệu."}</p> : <>
          <div className="menu-editor-fields">{model.fields.filter((field) => primaryFields.has(field.name)).map(renderField)}</div>
          <details open><summary>Thuộc tính kỹ thuật</summary><div className="menu-editor-fields technical">
            {model.fields.filter((field) => !primaryFields.has(field.name)).map(renderField)}
          </div></details>
        </>}
      </section>
      <aside className="menu-editor-resources">
        <h3>Controller và tài nguyên</h3>
        <div className="menu-editor-controller"><span>Controller</span><strong>{resources?.controllers.join(", ") || "Chưa xác định"}</strong></div>
        <div className="menu-editor-controller"><span>Table Dir</span><strong>{resources?.tables.join(", ") || "Chưa khai báo"}</strong></div>
        <div className="resource-list">
          {resources?.resources.map((resource) => <div className="resource-card" key={resource.kind}>
            <FileCode2 size={17} /><div><strong>{resource.kind}</strong><span className={`resource-status ${resource.status}`}>{resource.status === "found" ? "Có file" : resource.status === "multiple" ? `${resource.paths.length} file` : "Thiếu file"}</span>
              {resource.paths.map((path) => <button key={path} title={path} onClick={() => onOpenPath(root + (root.endsWith("\\") ? "" : "\\") + path)}>{path}</button>)}
            </div>
          </div>)}
          {!resources && <p className="menu-editor-muted"><FolderSearch size={18} />{busy ? "Đang dò controller…" : "Chưa có kết quả."}</p>}
        </div>
      </aside>
    </div>
    <div className="modal-footer"><span className="menu-editor-dirty">{dirty ? "Có thay đổi chưa lưu" : "Không có thay đổi"}</span><Button onClick={onClose}>Đóng</Button><Button variant="primary" onClick={() => void save()} disabled={busy || !dirty}><Save size={15} />Lưu</Button></div>
  </Modal></div>;
}
