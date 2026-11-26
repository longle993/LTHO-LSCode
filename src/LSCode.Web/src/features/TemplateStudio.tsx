import { useEffect, useRef, useState } from "react";
import { readClipboard, request, writeClipboard } from "../bridge";
import { Button, DataGrid, IconButton, Modal, Select, WorkbenchNotice } from "../ui";
import { Check, GripVertical, Pencil, Plus, X } from "lucide-react";
import { useTask } from "./useTask";
import "./templateStudio.css";

type Field = { name: string; path: string; type: string; key: boolean; required: boolean;
  defaultJson?: string; maxLength?: number | null; allowedValuesText?: string; constantJson?: string };
type KeyPair = { column: string; references: string };
type ParentKey = { strategy: string; column: string; references: string; pairs: KeyPair[] };
type StudioTable = { name: string; from: string; mode: string; parent?: string; rowKey?: boolean;
  parentKey?: ParentKey; columns: Field[] };
type Template = { templateCode: string; execute: string; sampleJson: string; tables: StudioTable[];
  id?: string; displayName?: string; description?: string; version?: number; transactionEnabled?: boolean };
type Project = { projectName: string; templates: Template[] };
type SaveIssue = { templateCode: string; table: string; field: string; tableIndex?: number | null;
  fieldIndex?: number | null; location: string; message: string };
type SaveResult = { valid: boolean; issues: SaveIssue[] };
type Preview = { json?: string; diagnostics: { message: string; table: string; field: string; isWarning: boolean }[];
  tables?: Record<string, Record<string, unknown>[]>; truncated?: boolean };
const views = [["fields", "Thiết kế"], ["json", "JSON Preview"], ["mapping", "Test Mapping"]] as const;
const sqlTypes = ["char", "varchar", "nvarchar", "smalldatetime", "bit", "numeric", "int"];

export function TemplateStudio({ confirm, confirming, onDirty, onBusy }: {
  confirm: (message: string) => Promise<boolean>;
  confirming: boolean;
  onDirty: (dirty: boolean) => void;
  onBusy: (busy: boolean) => void;
}) {
  const [project, setProject] = useState<Project>();
  const [savedProject, setSavedProject] = useState<Project>();
  const [dirty, setDirty] = useState(false);
  const [history, setHistory] = useState<Project[]>([]);
  const [redo, setRedo] = useState<Project[]>([]);
  const [checked, setChecked] = useState<number[]>([]);
  const [autosave, setAutosave] = useState(true);
  const [autoSaving, setAutoSaving] = useState(false);
  const [autoSaveError, setAutoSaveError] = useState("");
  const [saveIssues, setSaveIssues] = useState<SaveIssue[]>([]);
  const [issueTarget, setIssueTarget] = useState<{ location: string; field?: number }>();
  const latestProject = useRef<Project | undefined>(undefined);
  const attemptedAutoSave = useRef<Project | undefined>(undefined);
  const pendingAutoSave = useRef<Promise<void> | undefined>(undefined);
  const [templateIndex, selectTemplate] = useState(0);
  const [tableIndex, selectTable] = useState(0);
  const [view, setView] = useState<"fields" | "json" | "mapping">("fields");
  const [preview, setPreview] = useState<Preview>();
  const [status, setStatus] = useState("");
  const [newTemplateCode, setNewTemplateCode] = useState<string>();
  const [newTemplateError, setNewTemplateError] = useState("");
  const [fieldIndex, selectField] = useState<number>();
  const [settingsOpen, setSettingsOpen] = useState(true);
  const [resultName, selectResult] = useState("");
  const [proposal, setProposal] = useState<StudioTable[]>();
  const [rename, setRename] = useState<{ name: string; field?: number }>();
  const [draggingField, setDraggingField] = useState<number>();
  const [dragTarget, setDragTarget] = useState<number>();
  const renameInput = useRef<HTMLInputElement>(null);
  const studioRoot = useRef<HTMLDivElement>(null);
  const task = useTask();
  useEffect(() => onBusy(task.busy), [onBusy, task.busy]);
  const template = project?.templates[templateIndex];
  const table = template?.tables[tableIndex];
  const templateIssues = saveIssues.filter((issue) => issue.templateCode === template?.templateCode);
  const tableIssues = templateIssues.filter((issue) => issue.table === table?.name &&
    (issue.tableIndex == null || issue.tableIndex === tableIndex));
  const fieldIssues = (index: number) => tableIssues.filter((issue) => issue.field === table?.columns[index]?.name &&
    (issue.fieldIndex != null || issue.field.length > 0) &&
    (issue.fieldIndex == null || issue.fieldIndex === index));
  const parent = template?.tables.find((item) => item !== table &&
    item.name.replace(/^#/, "").toLowerCase() === table?.parent?.replace(/^#/, "").toLowerCase());
  const parentKeyOptions = parent?.columns.filter((item) => item.key).map((item) => item.name) ?? [];
  const parentKey = table?.parentKey ?? { strategy: parent?.rowKey ? "rowKey" : "businessKey", column: "", references: "", pairs: [] };
  const relationshipPairs = parentKey.pairs?.length ? parentKey.pairs : parentKeyOptions.length
    ? [{ column: parentKey.column || parentKeyOptions[0], references: parentKey.references || parentKeyOptions[0] }] : [];
  const field = fieldIndex === undefined ? undefined : table?.columns[fieldIndex];
  const baseType = field?.type.split("(")[0].toLowerCase() ?? "";
  const isText = ["char", "varchar", "nvarchar", "string"].includes(baseType);
  const resultNames = Object.keys(preview?.tables ?? {});
  const resultRows = preview?.tables?.[resultName] ?? preview?.tables?.[resultNames[0]] ?? [];
  const resultColumns = [...new Set(resultRows.flatMap((row) => Object.keys(row)))];
  useEffect(() => { setChecked([]); selectField(undefined); setRename(undefined); }, [tableIndex, templateIndex]);
  useEffect(() => {
    if (!issueTarget) return;
    if (fieldIndex !== issueTarget.field) {
      selectField(issueTarget.field);
      return;
    }
    const frame = requestAnimationFrame(() => {
      const location = issueTarget.location;
      const row = studioRoot.current?.querySelector(`#studio-field-${issueTarget.field}`);
      const scope = location === "Property" ? row : studioRoot.current;
      const exact = Array.from(scope
        ?.querySelectorAll<HTMLElement>("[data-studio-location]") ?? [])
        .find((element) => element.dataset.studioLocation === location);
      const target = exact ?? row ?? studioRoot.current?.querySelector(".studio-table-settings")
        ?? studioRoot.current?.querySelector(".studio-settings summary");
      if (target instanceof HTMLElement) {
        target.focus({ preventScroll: true });
        target.scrollIntoView({ block: "nearest", inline: "nearest" });
      }
      setIssueTarget(undefined);
    });
    return () => cancelAnimationFrame(frame);
  }, [issueTarget, fieldIndex]);
  useEffect(() => {
    renameInput.current?.focus();
    renameInput.current?.select();
  }, [rename?.field, !!rename]);
  useEffect(() => () => { latestProject.current = undefined; }, []);

  function apply(next: Project) {
    latestProject.current = next;
    setProject(next);
    const changed = next !== savedProject && JSON.stringify(next) !== JSON.stringify(savedProject);
    const hasPendingChanges = changed || (pendingAutoSave.current !== undefined && next !== attemptedAutoSave.current);
    setDirty(hasPendingChanges);
    onDirty(hasPendingChanges);
    setPreview(undefined);
    setStatus("");
  }
  function change(next: Project) {
    if (project) setHistory((old) => [...old.slice(-49), project]);
    setRedo([]);
    apply(next);
  }
  function undo() {
    if (!project || !history.length) return;
    setRedo((old) => [...old, project]);
    setHistory(history.slice(0, -1));
    apply(history[history.length - 1]);
    const snapshot = history[history.length - 1];
    const index = Math.min(templateIndex, Math.max(0, snapshot.templates.length - 1));
    selectTemplate(index); selectTable(Math.min(tableIndex, Math.max(0, (snapshot.templates[index]?.tables.length ?? 0) - 1)));
    setChecked([]); selectField(undefined); setRename(undefined);
  }
  function redoChange() {
    if (!project || !redo.length) return;
    setHistory((old) => [...old.slice(-49), project]);
    setRedo(redo.slice(0, -1));
    apply(redo[redo.length - 1]);
    const snapshot = redo[redo.length - 1];
    const index = Math.min(templateIndex, Math.max(0, snapshot.templates.length - 1));
    selectTemplate(index); selectTable(Math.min(tableIndex, Math.max(0, (snapshot.templates[index]?.tables.length ?? 0) - 1)));
    setChecked([]); selectField(undefined); setRename(undefined);
  }
  function editTemplate(update: (value: Template) => Template) {
    if (!project) return;
    change({ ...project, templates: project.templates.map((value, index) =>
      index === templateIndex ? update(value) : value) });
  }
  function editTable(update: (value: StudioTable) => StudioTable) {
    editTemplate((value) => ({ ...value, tables: value.tables.map((item, index) =>
      index === tableIndex ? update(item) : item) }));
  }
  function editField(index: number, patch: Partial<Field>) {
    editTable((value) => ({ ...value, columns: value.columns.map((item, i) =>
      i === index ? { ...item, ...patch } : item) }));
  }
  const open = (method: "open" | "create" | "recover") => task.run(async () => {
    if (dirty && !await confirm("Bỏ thay đổi chưa lưu để mở project khác?")) return;
    const folder = await request<string | null>("workspace.folder");
    if (!folder) return;
    if (method === "recover" && !await confirm("Khôi phục các tệp về trước lượt lưu bị gián đoạn?")) return;
    await pendingAutoSave.current;
    const opened = await request<{ project: Project; draft?: Project; skippedTemplates?: string[] }>(
      "templateStudio." + method, { folder },
    );
    const selectedProject = opened.draft && await confirm("Mở bản nháp Studio đã lưu?") ? opened.draft : opened.project;
    const openedDraft = selectedProject === opened.draft;
    latestProject.current = selectedProject;
    attemptedAutoSave.current = openedDraft ? selectedProject : undefined;
    setAutoSaveError("");
    setSaveIssues([]);
    setProject(selectedProject);
    setSavedProject(opened.project);
    setHistory([]); setRedo([]); setChecked([]);
    selectTemplate(0); selectTable(0); setView("fields");
    setPreview(undefined); setDirty(openedDraft); onDirty(openedDraft);
    setStatus(opened.skippedTemplates?.length
      ? `Không đọc được template: ${opened.skippedTemplates.join(", ")}.`
      : "");
  });
  function markSaved(snapshot: Project) {
    const current = latestProject.current;
    if (!current) return;
    setSavedProject(snapshot);
    const changed = current !== snapshot && JSON.stringify(current) !== JSON.stringify(snapshot);
    setDirty(changed); onDirty(changed);
  }
  const save = () => task.run(async () => {
    if (!project) return;
    await pendingAutoSave.current;
    attemptedAutoSave.current = project;
    setAutoSaveError("");
    const result = await request<SaveResult>("templateStudio.save", { project });
    setSaveIssues(result.issues);
    if (result.valid) markSaved(project);
    setStatus(result.valid ? "Đã lưu project." : "");
  });
  const saveTemplate = () => task.run(async () => {
    if (!project || !template) return;
    await pendingAutoSave.current;
    setAutoSaveError("");
    const result = await request<SaveResult & { project: Project }>("templateStudio.saveTemplate", { project, template: templateIndex });
    if (result.valid) markSaved(result.project);
    setSaveIssues((previous) => result.valid ? previous.filter((item) => item.templateCode !== template.templateCode) : result.issues);
    setStatus(result.valid
      ? `Đã lưu riêng template ${template.templateCode}.${JSON.stringify(project) !== JSON.stringify(result.project) ? " Các template khác vẫn còn thay đổi." : ""}`
      : "");
  });
  useEffect(() => {
    if (!autosave || !project || !dirty || autoSaving || task.busy || confirming || attemptedAutoSave.current === project) return;
    const snapshot = project;
    const timer = window.setTimeout(() => {
      if (latestProject.current !== snapshot) return;
      attemptedAutoSave.current = snapshot;
      setAutoSaving(true);
      pendingAutoSave.current = request<SaveResult>("templateStudio.save", { project: snapshot })
        .then((result) => {
          if (!latestProject.current) return;
          if (result.valid) {
            markSaved(snapshot);
            setAutoSaveError("");
            setSaveIssues([]);
          } else if (latestProject.current === snapshot) {
            setAutoSaveError("");
            setSaveIssues(result.issues);
          }
        })
        .catch((error: Error) => {
          if (latestProject.current === snapshot) setAutoSaveError(`Tự lưu thất bại. ${error.message}`);
        })
        .finally(() => {
          pendingAutoSave.current = undefined;
          if (latestProject.current) setAutoSaving(false);
        });
    }, 800);
    return () => window.clearTimeout(timer);
  }, [autosave, project, dirty, autoSaving, task.busy, confirming]);
  function goToIssue(issue: SaveIssue) {
    const index = project?.templates.findIndex((item) => item.templateCode === issue.templateCode) ?? -1;
    if (!project || index < 0) return;
    const tables = project.templates[index].tables;
    const affectedTable = issue.tableIndex != null && tables[issue.tableIndex]?.name === issue.table
      ? issue.tableIndex : tables.findIndex((item) => item.name === issue.table);
    const columns = tables[affectedTable]?.columns ?? [];
    const affectedField = issue.fieldIndex != null && columns[issue.fieldIndex]?.name === issue.field
      ? issue.fieldIndex : columns.findIndex((item) => item.name === issue.field);
    selectTemplate(index);
    selectTable(Math.max(0, affectedTable));
    setView("fields");
    setRename(undefined);
    if (affectedTable < 0) setSettingsOpen(true);
    setIssueTarget({ location: issue.location, field: affectedField < 0 ? undefined : affectedField });
  }
  const editNative = (method: string, args: object) => task.run(async () => {
    const updated = await request<Template>("templateStudio." + method, { template, table: tableIndex, ...args });
    editTemplate(() => updated);
    return true;
  });
  const synchronize = (next: Template, generateMissing = true) => task.run(async () => {
    const updated = await request<Template>("templateStudio.synchronize", {
      template: next, table: tableIndex, generateMissing,
    });
    editTemplate(() => updated);
  });
  const inspect = (mode: "json" | "mapping") => task.run(async () => {
    setPreview(undefined);
    setView(mode);
    selectResult("");
    setPreview(await request<Preview>("templateStudio." + mode, { template }));
  });
  function addField() {
    if (!table) return;
    let number = 1;
    while (table.columns.some((item) => item.name.toLowerCase() === "field" + number)) number++;
    editTable((value) => ({ ...value, columns: [...value.columns,
      { name: "Field" + number, path: "$.field" + number, type: "nvarchar(50)", key: false, required: false }] }));
    selectField(table.columns.length);
    requestAnimationFrame(() => document.getElementById("studio-field-" + table.columns.length)?.scrollIntoView({ block: "nearest" }));
  }
  function updateProperty(index: number, value: string) {
    const trimmed = value.trim();
    const path = !trimmed || trimmed.startsWith("$") || trimmed.startsWith("@") ? trimmed
      : trimmed.startsWith(".") || trimmed.startsWith("[") ? "$" + trimmed : "$." + trimmed;
    editField(index, { path, constantJson: "" });
  }
  function moveField(source: number, target: number) {
    if (!table || source === target || source < 0 || target < 0 || source >= table.columns.length || target >= table.columns.length) return;
    const selected = fieldIndex === undefined ? undefined : table.columns[fieldIndex];
    const selectedChecks = checked.map((index) => table.columns[index]);
    const columns = [...table.columns];
    const [moving] = columns.splice(source, 1);
    columns.splice(target, 0, moving);
    editTable((value) => ({ ...value, columns }));
    selectField(selected ? columns.indexOf(selected) : undefined);
    setChecked(selectedChecks.map((item) => columns.indexOf(item)).filter((index) => index >= 0).sort((a, b) => a - b));
    setDraggingField(undefined); setDragTarget(undefined);
  }
  function updateKeyPairs(pairs: KeyPair[]) {
    if (!table) return;
    const next = pairs.length === 1
      ? { ...parentKey, strategy: "businessKey", column: pairs[0].column, references: pairs[0].references, pairs: [] }
      : { ...parentKey, strategy: "businessKey", pairs };
    editTable((value) => ({ ...value, parentKey: next }));
  }
  function activate(next: typeof view) {
    if (next === view) return;
    setRename(undefined);
    if (next === "json") void inspect("json");
    else { setView(next); setPreview(undefined); }
  }
  function renameControl() {
    if (!rename) return null;
    return <form className="studio-inline" onSubmit={(event) => {
      event.preventDefault();
      const name = rename.name.trim();
      const action = rename.field === undefined ? "renameTable" : "renameField";
      void editNative(action, { name, field: rename.field }).then((saved) => { if (saved) setRename(undefined); });
    }} onKeyDown={(event) => { if (event.key === "Escape") { event.stopPropagation(); setRename(undefined); } }}>
      <input ref={renameInput} aria-label={rename.field === undefined ? "Tên bảng mới" : "Tên field mới"}
        value={rename.name} onChange={(event) => setRename({ ...rename, name: event.target.value })} />
      <IconButton type="submit" label="Áp dụng tên" disabled={!rename.name.trim()}><Check size={14} /></IconButton>
      <IconButton type="button" label="Hủy đổi tên" onClick={() => setRename(undefined)}><X size={14} /></IconButton>
    </form>;
  }
  return <div ref={studioRoot} className="studio-workspace" aria-busy={task.busy}>
    <div className="tool-toolbar studio-project-toolbar">
      <Button disabled={task.busy} onClick={() => void open("open")}>Mở project Studio</Button>
      <Button disabled={task.busy} onClick={() => void open("create")}>Tạo project</Button>
      <Button disabled={task.busy} onClick={() => void open("recover")}>Khôi phục lượt lưu</Button>
      {project && <strong>{project.projectName}{dirty ? " *" : ""}</strong>}
      {project && <Button variant="primary" disabled={!dirty || task.busy} onClick={() => void save()}>Lưu project</Button>}
      {project && <>
        <Button disabled={!template || task.busy} onClick={() => void saveTemplate()}>Lưu template</Button>
        <Button disabled={!template || task.busy} onClick={() => void task.run(async () => {
          if (!template) return;
          const text = await request<string>("templateStudio.exportTemplate", { template });
          const path = await request<string | null>("text.export", { name: template.templateCode + ".json", text });
          if (path) setStatus(`Đã xuất template ${template.templateCode}.`);
        })}>Xuất template</Button>
        <Button disabled={task.busy} onClick={() => void task.run(async () => {
          const folder = await request<string | null>("workspace.folder");
          if (!folder) return;
          await request("templateStudio.export", { project, folder });
          setStatus("Đã xuất project.");
        })}>Xuất project</Button>
        <Button disabled={!history.length || task.busy} onClick={undo}>Hoàn tác</Button>
        <Button disabled={!redo.length || task.busy} onClick={redoChange}>Làm lại</Button>
        <label><input type="checkbox" checked={autosave} onChange={(event) => setAutosave(event.target.checked)} /> Tự lưu</label>
      </>}
    </div>
    {!project && <p className="studio-message muted">Chọn thư mục có project.json của Dynamic Template Studio.</p>}
    {project && <div className="studio-session">
      <fieldset disabled={task.busy} className="studio-session-controls">
      <div className="tool-toolbar studio-template-toolbar">
        <Select label="Template" value={String(templateIndex)} disabled={!project.templates.length} floating
          searchOptions="Tìm template"
          options={project.templates.map((item, index) => ({ value: String(index), label: item.templateCode }))}
          onChange={(value) => { selectTemplate(Number(value)); selectTable(0); setPreview(undefined); setView("fields"); }} />
        <Button onClick={() => { setNewTemplateError(""); setNewTemplateCode(""); }}>Thêm template</Button>
      </div>
      </fieldset>
      {template && <div className="studio-tabs" role="tablist" aria-label="Chế độ Studio">
        {views.map(([id, label], index) => <Button key={id} id={`studio-view-${id}`} role="tab" variant="ghost"
          aria-selected={view === id} aria-controls={`studio-pane-${id}`} tabIndex={view === id ? 0 : -1} disabled={task.busy}
          onClick={() => activate(id)} onKeyDown={(event) => {
            const next = event.key === "Home" ? 0 : event.key === "End" ? views.length - 1
              : event.key === "ArrowRight" ? (index + 1) % views.length
              : event.key === "ArrowLeft" ? (index + views.length - 1) % views.length : -1;
            if (next < 0) return;
            event.preventDefault();
            document.getElementById(`studio-view-${views[next][0]}`)?.focus();
            activate(views[next][0]);
          }}>{label}</Button>)}
      </div>}
      <section className="studio-view" id={`studio-pane-${view}`} role={template ? "tabpanel" : undefined}
        aria-labelledby={template ? `studio-view-${view}` : undefined}>
      {!template && <p className="muted">Project chưa có template.</p>}
      {saveIssues.length > 0 && <section className="studio-save-issues" aria-label="Lỗi khi lưu">
        <strong role="status">Đã lưu bản nháp. Cần sửa {saveIssues.length} lỗi để lưu bản chính.</strong>
        <ul>
          {saveIssues.map((issue, index) => <li key={index}>
            <Button variant="ghost" disabled={task.busy} onClick={() => goToIssue(issue)}>
              <strong>{[issue.templateCode, issue.table, issue.field, issue.location].filter(Boolean).join(" › ")}</strong>
              <span>{issue.message}</span>
            </Button>
          </li>)}
        </ul>
      </section>}
      {template && view === "fields" && <div className="studio-designer">
        <aside className="studio-design-sidebar">
          <nav aria-label="Bảng" className="studio-tables">
            <strong>Bảng</strong>
            {template.tables.map((item, index) => <Button key={index} aria-pressed={index === tableIndex} disabled={task.busy}
              variant={index === tableIndex ? "secondary" : "ghost"} onClick={() => selectTable(index)}>{item.name}</Button>)}
            <Button data-studio-location="Bảng" disabled={task.busy} onClick={() => {
              let number = 1;
              while (template.tables.some((item) => item.name.replace(/^#/, "").toLowerCase() === "table" + number)) number++;
              editTemplate((value) => ({ ...value, tables: [...value.tables, { name: "#Table" + number, from: "$", mode: "array", columns: [] }] }));
              selectTable(template.tables.length);
            }}>Thêm bảng</Button>
          </nav>
          <details className="studio-settings" open={settingsOpen} onToggle={(event) => setSettingsOpen(event.currentTarget.open)}>
            <summary data-studio-location="Template">Thuộc tính template</summary>
            <fieldset className="studio-form" disabled={task.busy}>
              <label>Tên hiển thị<input value={template.displayName ?? ""} onChange={(event) => editTemplate((value) => ({ ...value, displayName: event.target.value }))} /></label>
              <label>Execute<input data-studio-location="Execute" aria-invalid={templateIssues.some((issue) => issue.location === "Execute")}
                value={template.execute} onChange={(event) => editTemplate((value) => ({ ...value, execute: event.target.value }))} /></label>
              <label>Mô tả<input value={template.description ?? ""} onChange={(event) => editTemplate((value) => ({ ...value, description: event.target.value }))} /></label>
              <label>Phiên bản<input data-studio-location="Phiên bản" type="number" min={1} value={template.version ?? 1}
                onChange={(event) => { if (event.target.valueAsNumber >= 1 && Number.isInteger(event.target.valueAsNumber))
                  editTemplate((value) => ({ ...value, version: event.target.valueAsNumber })); }} /></label>
              <label className="studio-check"><input type="checkbox" checked={template.transactionEnabled ?? true}
                onChange={(event) => editTemplate((value) => ({ ...value, transactionEnabled: event.target.checked }))} /> Transaction</label>
            </fieldset>
          </details>
        </aside>
        <div className="studio-editor">
          {!table && <p className="muted">Thêm bảng để khai báo nguồn JSON và field.</p>}
          {table && <>
          <fieldset className="studio-table-settings" tabIndex={-1} disabled={task.busy}>
            <div className="studio-name">{rename && rename.field === undefined ? renameControl() : <>
              <strong>{table.name}</strong>
              <IconButton label="Đổi tên bảng" onClick={() => setRename({ name: table.name })}><Pencil size={14} /></IconButton>
            </>}</div>
            <div className="studio-form">
            <label>From <input data-studio-location="From" aria-invalid={tableIssues.some((issue) => issue.location === "From")}
              value={table.from} placeholder={table.mode === "array" ? "VD: $.items[*]" : "VD: $ hoặc $.header"}
              onChange={(event) => editTable((value) => ({ ...value, from: event.target.value }))} /></label>
            <div className="studio-field-label"><span>Bảng cha</span><Select label="Bảng cha"
              value={String(template.tables.findIndex((item) => item.name.replace(/^#/, "").toLowerCase() === table.parent?.replace(/^#/, "").toLowerCase()))}
              options={[{ value: "-1", label: "Không có" }, ...template.tables.flatMap((item, index) =>
                index === tableIndex ? [] : [{ value: String(index), label: item.name }])]}
              onChange={(value) => void editNative("parent", { parent: Number(value) })} /></div>
            <div className="studio-field-label"><span>Chế độ nguồn</span><Select label="Chế độ nguồn" value={table.mode}
              options={[{ value: "array", label: "Array" }, { value: "object", label: "Object" }]}
              onChange={(mode) => editTable((value) => ({ ...value, mode }))} /></div>
            <label><input type="checkbox" checked={table.rowKey ?? false} onChange={(event) => {
              const next = { ...template, tables: template.tables.map((item, index) => index === tableIndex
                ? { ...item, rowKey: event.target.checked } : item) };
              void synchronize(next, true);
            }} /> RowKey</label>
            </div>
            {parent && <section className="studio-relationship" aria-label="Khóa cha con">
              <div className="studio-relationship-heading">
                <div><strong>Khóa cha–con</strong><p>{parent.name} → {table.name}</p></div>
                <span className="studio-relation-badge">{parent.rowKey ? "RowKey tự sinh" : "Business key"}</span>
              </div>
              {parent.rowKey ? <div className="studio-key-summary">
                <span>Cột con</span><code>{parentKey.column || `_${parent.name.replace(/^#/, "")}RowKey`}</code>
                <span>Tham chiếu cha</span><code>_RowKey</code>
              </div> : <>
                {!parentKeyOptions.length && <p className="studio-relation-warning">Bảng cha chưa có field Key. Hãy bật Key ở bảng {parent.name}.</p>}
                {relationshipPairs.map((pair, index) => <div className="studio-key-pair" key={`${index}:${pair.references}`}>
                  <label>Cột ở bảng con<input value={pair.column} onChange={(event) => updateKeyPairs(relationshipPairs.map((item, i) =>
                    i === index ? { ...item, column: event.target.value } : item))} /></label>
                  <div className="studio-field-label"><span>Khóa ở bảng cha</span><Select label={`Khóa cha ${index + 1}`}
                    value={pair.references} options={parentKeyOptions.map((name) => ({ value: name, label: name }))}
                    onChange={(references) => updateKeyPairs(relationshipPairs.map((item, i) =>
                      i === index ? { ...item, references } : item))} /></div>
                  {relationshipPairs.length > 1 && <Button variant="ghost" onClick={() => updateKeyPairs(relationshipPairs.filter((_, i) => i !== index))}>Xóa cặp</Button>}
                </div>)}
                {relationshipPairs.length < parentKeyOptions.length && <Button onClick={() => {
                  const references = parentKeyOptions.find((name) => !relationshipPairs.some((pair) => pair.references === name)) ?? parentKeyOptions[0];
                  updateKeyPairs([...relationshipPairs, { column: references, references }]);
                }}><Plus size={14} /> Thêm cặp khóa</Button>}
              </>}
            </section>}
          </fieldset>
          {field && fieldIndex !== undefined && <fieldset className="studio-inspector" disabled={task.busy}>
            <div className="studio-name"><strong>{field.name}</strong></div>
            <div className="studio-form">
              <div className="studio-field-label"><span>Kiểu SQL</span><Select label="Kiểu SQL" value={baseType}
                options={[...new Set([...sqlTypes, baseType])].filter(Boolean).map((value) => ({ value, label: value }))}
                onChange={(type) => {
                  if (type === baseType) return;
                  const text = ["char", "varchar", "nvarchar", "string"].includes(type);
                  editField(fieldIndex, { type: type === "numeric" ? "numeric(19,5)" : type,
                    ...(text ? {} : { maxLength: null, allowedValuesText: "" }) });
                }} /></div>
              {["numeric", "decimal"].includes(baseType) && <label>Precision, scale
                <input key={field.type} defaultValue={field.type.match(/\((.*)\)/)?.[1] ?? "19,5"}
                  onBlur={(event) => { if (event.target.value !== event.target.defaultValue)
                    editField(fieldIndex, { type: baseType + "(" + event.target.value.replace(/\s+/g, "") + ")" }); }} />
              </label>}
              <label>Default (JSON)<input data-studio-location="Default (JSON)"
                aria-invalid={fieldIssues(fieldIndex).some((issue) => issue.location === "Default (JSON)")} value={field.defaultJson ?? ""}
                onChange={(event) => editField(fieldIndex, { defaultJson: event.target.value })} /></label>
              {isText && <>
                <label>Max length<input data-studio-location="Max length" type="number" min={1} value={field.maxLength ?? field.type.match(/\((\d+)\)/)?.[1] ?? ""}
                  onChange={(event) => editField(fieldIndex, { maxLength: event.target.valueAsNumber || null, type: baseType })} /></label>
                <label>Giá trị chấp nhận<input value={field.allowedValuesText ?? ""} placeholder="1,2,0"
                  onChange={(event) => editField(fieldIndex, { allowedValuesText: event.target.value })} /></label>
              </>}
            </div>
          </fieldset>}
          <fieldset className="tool-toolbar studio-field-toolbar" disabled={task.busy}>
            <Button onClick={addField}><Plus size={14} /> Thêm field</Button>
            <Button disabled={!checked.length} onClick={() => void task.run(async () => {
              await writeClipboard(await request<string>("templateStudio.copyFields", { template, table: tableIndex, fields: checked }));
              setStatus("Đã sao chép field.");
            })}>Sao chép field</Button>
            <Button onClick={() => void task.run(async () => {
              const text = await readClipboard();
              const updated = await request<Template>("templateStudio.pasteFields", { template, table: tableIndex, text });
              editTemplate(() => updated);
            })}>Dán field</Button>
          </fieldset>
          <div className="studio-grid-scroll" tabIndex={0} aria-label="Danh sách field">
          <table className="studio-grid"><thead><tr>
            <th aria-label="Sắp xếp"></th><th>Chọn</th><th>Field</th><th>Property</th><th>Kiểu SQL</th><th>Key</th><th>Required</th>
          </tr></thead><tbody>
            {table.columns.map((field, index) => <tr key={field.name + ":" + index} id={`studio-field-${index}`}
              tabIndex={-1} aria-current={fieldIndex === index} data-invalid={fieldIssues(index).length > 0}
              data-current={fieldIndex === index} data-dragging={draggingField === index} data-drag-target={dragTarget === index}
              onClick={() => selectField(index)}
              onDragOver={(event) => { if (draggingField === undefined) return; event.preventDefault(); setDragTarget(index); }}
              onDrop={(event) => { event.preventDefault(); if (draggingField !== undefined) moveField(draggingField, index); }}>
              <td className="studio-drag-cell"><button type="button" draggable={!task.busy} aria-label={`Kéo sắp xếp ${field.name}. Alt mũi tên lên xuống để di chuyển`}
                onDragStart={(event) => { setDraggingField(index); event.dataTransfer.effectAllowed = "move"; event.dataTransfer.setData("text/plain", String(index)); }}
                onDragEnd={() => { setDraggingField(undefined); setDragTarget(undefined); }}
                onKeyDown={(event) => { if (!event.altKey || !["ArrowUp", "ArrowDown"].includes(event.key)) return; event.preventDefault(); moveField(index, index + (event.key === "ArrowUp" ? -1 : 1)); }}>
                <GripVertical size={15} />
              </button></td>
              <td><input type="checkbox" aria-label={`Chọn ${field.name}`} checked={checked.includes(index)} disabled={task.busy}
                onChange={() => setChecked((old) => old.includes(index) ? old.filter((i) => i !== index) : [...old, index].sort((a, b) => a - b))} /></td>
              <td><div className="studio-name">{rename?.field === index ? renameControl() : <>
                <Button variant="ghost" aria-pressed={fieldIndex === index} title={field.name}>{field.name}</Button>
                <IconButton label={`Đổi tên field ${field.name}`} onClick={() => setRename({ name: field.name, field: index })}><Pencil size={14} /></IconButton>
              </>}</div></td>
              <td><input data-studio-location="Property" aria-invalid={fieldIssues(index).some((issue) => issue.location === "Property")}
                key={tableIndex + ":" + field.path} aria-label={`Property ${field.name}`} defaultValue={field.path.replace(/^\$\.(?=[^\[])/, "")}
                readOnly={task.busy} onBlur={(event) => {
                  if (event.target.value !== event.target.defaultValue) updateProperty(index, event.target.value);
                }} onKeyDown={(event) => { if (event.key === "Enter") event.currentTarget.blur(); }} /></td>
              <td>{field.type}</td>
              <td><input aria-label={`Key ${index + 1}`} type="checkbox" checked={field.key} disabled={task.busy}
                onChange={(event) => {
                  const next = { ...template, tables: template.tables.map((item, itemIndex) => itemIndex === tableIndex
                    ? { ...item, columns: item.columns.map((column, columnIndex) => columnIndex === index
                      ? { ...column, key: event.target.checked } : column) } : item) };
                  void synchronize(next, true);
                }} /></td>
              <td><input aria-label={`Required ${index + 1}`} type="checkbox" checked={field.required} disabled={task.busy}
                onChange={(event) => editField(index, { required: event.target.checked })} /></td>
            </tr>)}
          </tbody></table>
          {!table.columns.length && <p className="studio-message muted">Bảng chưa có field.</p>}
          </div>
          </>}
        </div>
      </div>}
      {template && view === "mapping" && <fieldset className="studio-preview" disabled={task.busy}>
        <label>Dữ liệu mẫu <textarea className="block w-full font-mono" rows={8} value={template.sampleJson}
          onChange={(event) => editTemplate((value) => ({ ...value, sampleJson: event.target.value }))} /></label>
        <div className="tool-toolbar">
          <Button variant="primary" onClick={() => void inspect("mapping")}>Chạy mapping</Button>
          <Button onClick={() => void task.run(async () => {
            const inferred = await request<Template>("templateStudio.infer", { sample: template.sampleJson });
            setProposal(inferred.tables);
          })}>Đề xuất bảng từ JSON</Button>
        </div>
      </fieldset>}
      {proposal && <Modal title="Bảng đề xuất từ JSON" onClose={() => setProposal(undefined)}>
        <div className="tool-body studio-proposal">
          {proposal.map((item, index) => <label key={index} className="studio-proposal-row">
            <span>{item.name} · {item.columns.length} field{item.parent ? " · Cha: " + item.parent : ""}</span>
            <span><input type="checkbox" checked={item.rowKey ?? false}
              onChange={(event) => setProposal(proposal.map((entry, i) => i === index ? { ...entry, rowKey: event.target.checked } : entry))} /> RowKey</span>
          </label>)}
          {!proposal.length && <p>JSON mẫu không có bảng đề xuất.</p>}
          <div className="tool-toolbar">
            <Button onClick={() => setProposal(undefined)}>Hủy</Button>
            <Button variant="primary" disabled={!proposal.length} onClick={async () => {
              if (template?.tables.length && !await confirm("Thay các bảng của template bằng bảng đề xuất?")) return;
              editTemplate((value) => ({ ...value, tables: proposal }));
              setProposal(undefined); selectTable(0); selectField(undefined); setChecked([]); setView("fields");
            }}>Áp dụng bảng đề xuất</Button>
          </div>
        </div>
      </Modal>}
      {view === "json" && <div className="tool-toolbar studio-preview">
        <Button onClick={() => void inspect("json")} disabled={task.busy}>Làm mới JSON</Button>
        <Button onClick={() => void task.run(() => writeClipboard(preview?.json ?? ""))} disabled={!preview?.json || task.busy}>Sao chép JSON</Button>
      </div>}
      {view !== "fields" && preview && <>
        {preview.diagnostics.map((item, index) => <p className={item.isWarning ? "muted" : "form-error"} key={index}>
          {[item.table, item.field, item.message].filter(Boolean).join(" · ")}
        </p>)}
        {preview.truncated && <p role="status">Kết quả đã đạt giới hạn preview.</p>}
        {preview.json && <textarea aria-label="Kết quả preview" className="studio-json" rows={14} readOnly value={preview.json} />}
        {resultNames.length > 0 && <div className="studio-mapping-results">
          <Select label="Bảng kết quả" value={resultName || resultNames[0]} options={resultNames.map((name) => ({ value: name, label: name }))}
            onChange={selectResult} />
          <DataGrid table={{ columns: resultColumns, rows: resultRows.map((row) => resultColumns.map((name) =>
            row[name] == null ? null : typeof row[name] === "object" ? JSON.stringify(row[name]) : String(row[name]))) }} rangeSelection />
        </div>}
      </>}
      </section>
    </div>}
    {project && newTemplateCode !== undefined && <Modal title="Thêm template" onClose={() => setNewTemplateCode(undefined)}>
      <form onSubmit={event => {
        event.preventDefault();
        const templateCode = newTemplateCode.trim();
        if (!/^[A-Za-z_][A-Za-z0-9_.-]*$/.test(templateCode) ||
          project.templates.some(item => item.templateCode.toLowerCase() === templateCode.toLowerCase())) {
          setNewTemplateError("Mã template sai định dạng hoặc đã tồn tại.");
          return;
        }
        change({ ...project, templates: [...project.templates,
          { templateCode, execute: "dbo.usp_Import", sampleJson: "{}", tables: [] }] });
        selectTemplate(project.templates.length); selectTable(0); setView("fields");
        setNewTemplateCode(undefined);
      }}>
        <div className="modal-copy">
          <label>Mã template <input autoFocus value={newTemplateCode}
            onChange={event => { setNewTemplateCode(event.target.value); setNewTemplateError(""); }} /></label>
          {newTemplateError && <p role="alert" className="form-error">{newTemplateError}</p>}
        </div>
        <div className="modal-footer">
          <Button type="button" onClick={() => setNewTemplateCode(undefined)}>Hủy</Button>
          <Button type="submit" variant="primary" disabled={!newTemplateCode.trim()}>Thêm</Button>
        </div>
      </form>
    </Modal>}
    {(status || task.error || autoSaveError) && <WorkbenchNotice error={task.error || autoSaveError} status={status}
      onClose={task.error ? task.clearError : autoSaveError ? () => setAutoSaveError("") : () => setStatus("")} />}
  </div>;
}
