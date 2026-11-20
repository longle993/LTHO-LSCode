import { useEffect, useState } from "react";
import { request } from "../bridge";
import { Button, Select, WorkbenchNotice } from "../ui";
import { useTask } from "./useTask";
import "./catalogSource.css";

type Discovery = {
  controllers: string[];
  tables: string[];
  resources: { kind: string; paths: string[] }[];
};
type Draft = { fingerprint: string; hasMain: boolean; files: { path: string; text: string }[] };
type Package = { folder: string; files: string[] };

export function CatalogSourceTool({ root, initialTemplate, onBusy, onDirty }: {
  root: string;
  initialTemplate: string;
  onBusy: (busy: boolean) => void;
  onDirty: (dirty: boolean) => void;
}) {
  const task = useTask();
  const [template, setTemplate] = useState(initialTemplate);
  const [controller, setController] = useState("");
  const [titleVi, setTitleVi] = useState("");
  const [titleEn, setTitleEn] = useState("");
  const [fieldMappings, setFieldMappings] = useState("");
  const [tables, setTables] = useState<Record<string, string>>({});
  const [source, setSource] = useState<Discovery>();
  const [draft, setDraft] = useState<Draft>();
  const [output, setOutput] = useState<Package>();
  const [selected, setSelected] = useState("");
  const [notice, setNotice] = useState("");
  const chosen = draft?.files.find(file => file.path === selected);

  useEffect(() => { onBusy(task.busy); }, [onBusy, task.busy]);
  const dirty = !output && Boolean(controller || titleVi || titleEn || fieldMappings || Object.keys(tables).length);
  useEffect(() => { onDirty(dirty); }, [onDirty, dirty]);

  function resetPreview() {
    setDraft(undefined);
    setOutput(undefined);
    task.clearError();
    setNotice("");
  }

  function configuration() {
    const fields = new Map<string, string>();
    for (const line of fieldMappings.split(/\r?\n/).map(value => value.trim()).filter(Boolean)) {
      const parts = line.split("=").map(value => value.trim());
      if (parts.length !== 2 || !parts[0] || !parts[1] || fields.has(parts[0].toLowerCase()))
        throw new Error("Ánh xạ cột phải có dạng cot_cu = cot_moi và mỗi cột xuất hiện một lần.");
      fields.set(parts[0].toLowerCase(), parts[1]);
    }
    return {
      root, template: template.trim(), controller: controller.trim(), titleVi: titleVi.trim(), titleEn: titleEn.trim(),
      tables: Object.fromEntries(Object.entries(tables).filter(([key, value]) => value.trim() && value.trim() !== key)
        .map(([key, value]) => [key, value.trim()])),
      fields: Object.fromEntries(fields),
    };
  }

  function discover() {
    setSource(undefined);
    setTables({});
    resetPreview();
    return task.run(async () => {
      setSource(await request<Discovery>("catalogSource.discover", { root, template: template.trim() }));
    });
  }

  function preview() {
    resetPreview();
    return task.run(async () => {
      const result = await request<Draft>("catalogSource.preview", configuration());
      setDraft(result);
      setSelected(result.files[0]?.path ?? "");
    });
  }

  function exportSource() {
    if (!draft) return;
    void task.run(async () => {
      const folder = await request<string | null>("workspace.folder");
      if (folder) {
        const result = await request<Package>("catalogSource.export", {
          ...configuration(), fingerprint: draft.fingerprint, folder,
        });
        setOutput(result);
        setNotice(`Đã xuất ${result.files.length} file vào ${result.folder}`);
      }
    });
  }

  if (!root) return <p className="catalog-source-empty">Mở dự án ERP hoặc thư mục source để chọn mẫu.</p>;
  return <div className="catalog-source" aria-busy={task.busy}>
    <div className="catalog-source-scroll"><div className="catalog-source-body">
      <section className="catalog-source-inputs">
        <div className="catalog-source-template">
          <label>Mẫu ERP
            <input placeholder="Ví dụ Main/mau.aspx hoặc SysId" value={template} disabled={task.busy}
              onChange={event => { setTemplate(event.target.value); setSource(undefined); setTables({}); resetPreview(); }} />
          </label>
          <Button onClick={() => void discover()} disabled={task.busy || !template.trim()}>Đọc mẫu</Button>
        </div>
        {source && <>
          <p className="catalog-source-meta">{source.controllers.join(", ")} · {source.resources.flatMap(resource => resource.paths).length} file</p>
          <label>Controller mới
            <input value={controller} disabled={task.busy} onChange={event => { setController(event.target.value); resetPreview(); }} />
          </label>
          <div className="catalog-source-titles">
            <label>Tên tiếng Việt
              <input value={titleVi} disabled={task.busy} onChange={event => { setTitleVi(event.target.value); resetPreview(); }} />
            </label>
            <label>Tên tiếng Anh
              <input value={titleEn} disabled={task.busy} onChange={event => { setTitleEn(event.target.value); resetPreview(); }} />
            </label>
          </div>
          <div className="catalog-source-mappings">
            <h3>Ánh xạ bảng</h3>
            {source.tables.length ? source.tables.map(table => <label key={table}>
              {table}<input value={tables[table] ?? ""} placeholder={table} disabled={task.busy}
                onChange={event => { setTables(previous => ({ ...previous, [table]: event.target.value })); resetPreview(); }} />
            </label>) : <p className="catalog-source-meta">Mẫu không khai báo bảng trong Dir.</p>}
          </div>
          <label>Ánh xạ cột <span className="catalog-source-meta">Mỗi dòng một cặp</span>
            <textarea rows={3} placeholder="ma_cu = ma_moi" value={fieldMappings} disabled={task.busy}
              onChange={event => { setFieldMappings(event.target.value); resetPreview(); }} />
          </label>
        </>}
      </section>
      <section className="catalog-source-preview" aria-label="Xem trước source">
        {draft ? <>
          <Select label="File xem trước" value={selected} onChange={setSelected}
            options={draft.files.map(file => ({ value: file.path, label: file.path }))} floating />
          {!draft.hasMain && <p className="catalog-source-meta">Mẫu dùng controller không có Main. Đăng ký page trong PageMetadata nếu dự án yêu cầu.</p>}
          {chosen && <pre tabIndex={0}>{chosen.text}</pre>}
        </> : <div className="catalog-source-files">
          <h3>{source ? "File của mẫu" : "Xem trước source"}</h3>
          {source ? <ul>{source.resources.flatMap(resource => resource.paths).map(path => <li key={path}>{path}</li>)}</ul>
            : <p className="catalog-source-meta">Đọc mẫu, nhập controller mới rồi chọn Xem trước.</p>}
        </div>}
      </section>
    </div></div>
    <div className="modal-footer">
      {task.busy && <span role="status" className="catalog-source-meta">Đang xử lý…</span>}
      {output && <Button disabled={task.busy} onClick={() => void task.run(() => request("workspace.reveal", { path: output.folder }))}>
        Mở thư mục kết quả
      </Button>}
      <Button disabled={task.busy || !source || !controller.trim()} onClick={() => void preview()}>Xem trước</Button>
      <Button variant="primary" disabled={task.busy || !draft} onClick={exportSource}>Xuất source</Button>
    </div>
    <WorkbenchNotice error={task.error} status={notice} onClose={() => { task.clearError(); setNotice(""); }} />
  </div>;
}
