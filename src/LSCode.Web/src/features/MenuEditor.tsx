import { useEffect, useMemo, useState } from "react";
import { FileCode2, Plus, RefreshCw, Save, Table2 } from "lucide-react";
import { operation, request, type Target } from "../bridge";
import { Button, Select } from "../ui";
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
  menu_id: "Mã command", link: "Main", parameter: "Tham số", icon_url: "Icon URL",
  status: "Trạng thái", icon: "Icon", sysid: "Controller/SysId", type: "Loại", syscode: "Syscode",
  msys: "Msys", target: "Target", xtype: "Xtype", edition: "Edition", expi_icon: "Expi icon",
};
const mainRows = [["wmenu_id0", "wmenu_id", "menu_id"], ["bar", "bar2"], ["link"], ["sysid"]];
const technicalRows = [["parameter", "syscode", "type", "status"], ["icon_url", "icon"]];
const orderedFields = new Set([...mainRows.flat(), ...technicalRows.flat()]);
const typeOptions = [{ value: "", label: "Trống" }, { value: "D", label: "D" }, { value: "V", label: "V" }];

export function MenuEditor({ target, command, root, onClose, onSaved, onOpenPath, onOpenTable, onDirtyChange, onBusyChange }: {
  onDirtyChange: (dirty: boolean) => void;
  onBusyChange: (busy: boolean) => void;
  target: Target;
  command: MenuCommand;
  root: string;
  onClose: () => void;
  onSaved: (id: string) => Promise<void> | void;
  onOpenPath: (path: string) => void;
  onOpenTable?: (table: string) => void;
}) {
  const [model, setModel] = useState<Model>();
  const [values, setValues] = useState<Record<string, string | null>>({});
  const [resources, setResources] = useState<ResourceResult>();
  const [resourceError, setResourceError] = useState("");
  const [resourceRevision, setResourceRevision] = useState(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [recordId, setRecordId] = useState(command.entry.id);
  const dirty = useMemo(() => !!model && JSON.stringify(values) !== JSON.stringify(model.values), [model, values]);

  useEffect(() => { onDirtyChange(dirty); onBusyChange(busy); }, [dirty, busy, onDirtyChange, onBusyChange]);

  async function load() {
    setBusy(true); setError(""); setNotice("");
    try {
      const id = recordId || command.entry.id;
      const loaded = await request<Model>("project.menu.details", { targetId: target.id, wmenuId: id });
      setModel(loaded); setValues(loaded.values); setRecordId(id);
    } catch (e) { setError(e instanceof Error ? e.message : String(e)); }
    finally { setBusy(false); }
  }
  useEffect(() => { void load(); }, [target.id, command.entry.id]);

  const link = model ? values.link ?? "" : command.entry.link;
  const sysId = model ? values.sysid ?? "" : command.entry.sysId;
  useEffect(() => {
    let active = true;
    let pending: ReturnType<typeof operation<ResourceResult>> | undefined;
    setResources(undefined); setResourceError("");
    const timer = window.setTimeout(() => {
      pending = operation<ResourceResult>("project.menu.resources", { root, link, sysId });
      void pending.promise.then(
        result => { if (active) setResources(result); },
        failure => { if (active) setResourceError(failure instanceof Error ? failure.message : String(failure)); },
      ).finally(() => { pending = undefined; });
    }, dirty ? 300 : 0);
    return () => {
      active = false;
      window.clearTimeout(timer);
      void pending?.cancel().catch(() => {});
    };
  }, [root, link, sysId, resourceRevision]);

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
      await onSaved(loaded.values.wmenu_id ?? command.entry.id);
      setNotice("Đã lưu menu.");
    } catch (e) { setError(e instanceof Error ? e.message : String(e)); }
    finally { setBusy(false); }
  }
  const renderField = (field: Field) => {
    const limit = field.maxLength > 0 ? (field.type.startsWith("n") ? field.maxLength / 2 : field.maxLength) : undefined;
    const value = values[field.name] ?? "";
    const disabled = busy || field.readOnly;
    const change = (next: string) => setValues(old => ({ ...old, [field.name]: next }));
    if (field.name === "type") {
      const options = typeOptions.some(option => option.value === value)
        ? typeOptions : [...typeOptions, { value, label: value }];
      return <div className="menu-editor-field" key={field.name}>
        <span>Loại</span>
        <Select label="Loại" value={value} options={options} disabled={disabled} onChange={change} floating />
      </div>;
    }
    if (field.name === "status") return <label key={field.name}>
      <span>Trạng thái</span>
      <span className="menu-editor-checkbox"><input type="checkbox" checked={value === "1" || value.toLowerCase() === "true"}
        disabled={disabled} onChange={event => change(event.target.checked ? "1" : "0")} /></span>
    </label>;
    return <label key={field.name}>
      <span>{labels[field.name] ?? field.name} <small>{field.type}{limit ? `(${limit})` : ""}</small></span>
      <input
        value={value}
        readOnly={field.readOnly || (model?.mode === "edit" && field.name === "wmenu_id")}
        disabled={busy}
        maxLength={limit}
        onChange={(event) => change(event.target.value)}
      />
    </label>;
  };
  const renderRow = (names: string[]) => {
    const fields = names.flatMap(name => model?.fields.find(field => field.name === name) ?? []);
    if (!fields.length) return null;
    return <div className={`menu-editor-row columns-${fields.length}`} key={names[0]}>{fields.map(renderField)}</div>;
  };

  return <div className="menu-editor-shell" aria-label={model?.mode === "new" ? "Thêm menu ERP" : "Thông tin menu ERP"}>
    <div className="menu-editor-toolbar">
      <div><strong>{command.entry.caption}</strong><span>{target.database} · {target.server}</span></div>
      <Button onClick={() => { if (!dirty || window.confirm("Bỏ thay đổi chưa lưu và tải lại menu?")) {
        void load(); setResourceRevision(value => value + 1);
      } }} disabled={busy}><RefreshCw size={15} />Tải lại</Button>
      <Button onClick={() => void createNew()} disabled={busy || dirty}><Plus size={15} />Thêm mới</Button>
      <Button variant="primary" onClick={() => void save()} disabled={busy || !model?.canSave || !dirty}><Save size={15} />Lưu</Button>
      <Button onClick={onClose} disabled={busy}>Đóng</Button>
    </div>
    {(error || notice) && <div role="status" className={`menu-editor-message ${error ? "error" : "success"}`}>{error || notice}</div>}
    <div className="menu-editor-body" aria-busy={busy}>
      <section className="menu-editor-form">
        <h3>Thông tin menu</h3>
        {!model ? <p className="menu-editor-muted">{busy ? "Đang tải thông tin…" : "Không có dữ liệu."}</p> : <>
          <div className="menu-editor-fields">{mainRows.map(renderRow)}</div>
          <details open><summary>Thuộc tính kỹ thuật</summary><div className="menu-editor-fields technical">
            {technicalRows.map(renderRow)}
            <div className="menu-editor-row columns-2">{model.fields.filter(field => !orderedFields.has(field.name)).map(renderField)}</div>
          </div></details>
        </>}
      </section>
      <aside className="menu-editor-resources">
        <h3>Controller và tài nguyên</h3>
        <div className="menu-editor-inspection">
          <div className="menu-editor-controller"><span>Thay đổi</span><strong role="status">{dirty ? "Có thay đổi chưa lưu" : "Không có thay đổi"}</strong></div>
          <div className="menu-editor-controller"><span>Controller</span><strong>{resources ? resources.controllers.join(", ") || "Chưa xác định" : "—"}</strong></div>
          <div className="menu-editor-controller"><span>Table Dir</span><div className="menu-editor-table-list">
            {resources?.tables.length ? resources.tables.map(table => <button key={table} disabled={!onOpenTable}
              title={onOpenTable ? "Mở cấu trúc và dữ liệu" : "Dự án chưa có kết nối App"}
              onClick={() => onOpenTable?.(table)}><Table2 size={14} />{table}</button>) :
              <strong>{resources ? "Chưa khai báo" : "—"}</strong>}
          </div></div>
        </div>
        {resources ? <div className="menu-editor-file-panel">
          <h4>File liên quan</h4>
          {resources.resources.map(resource => <div className="menu-editor-file-group" key={resource.kind}>
            <strong>{resource.kind}</strong>
            {resource.paths.length ? <ul>{resource.paths.map(path => <li key={path}>
              <button title={path} onClick={() => onOpenPath(root + (root.endsWith("\\") ? "" : "\\") + path)}>
                <FileCode2 size={15} /><span>{path}</span>
              </button>
            </li>)}</ul> : <span className="menu-editor-muted">Thiếu file</span>}
          </div>)}
        </div> : <p role="status" className="menu-editor-muted">{resourceError || "Đang dò controller và file…"}</p>}
      </aside>
    </div>
  </div>;
}
