import { useEffect, useMemo, useRef, useState } from "react";
import { FileText, PackageOpen, Plus, Save, Trash2 } from "lucide-react";
import { request } from "../bridge";
import ControllerExplorer from "../ControllerExplorer";
import { Button } from "../ui";

export type AdvanceNoteMenu = {
  id: string;
  caption: string;
  sysId: string;
  link: string;
  sequence?: number;
};

export type AdvanceNoteDataCapture = {
  sequence: number;
  database: "App" | "Sys";
  table: string;
  where: string;
};

type AdvanceNoteSql = { objects: string; data: string; topScript: string };
type AdvanceNoteItem = {
  id: string;
  title: string;
  status: string;
  details: string;
  menuId: string;
  menuCaption: string;
  sysId: string;
  link: string;
  sourceFiles: string[];
  updatedAt: string;
  sql?: Record<string, AdvanceNoteSql>;
};

type AdvanceNoteDocument = { projectId: string; items: AdvanceNoteItem[]; filePath: string };
type SourceGroup = { link: string; sysId?: string; files: string[] };
type UpdateResult = { outputPath: string; fileCount: number; scriptFiles: string[] };
const emptySql = (): AdvanceNoteSql => ({ objects: "", data: "", topScript: "" });
const key = (value: string) => value.replaceAll("\\", "/").toLowerCase();

function isAllowedSource(path: string) {
  const parts = path.replaceAll("\\", "/").split("/").map(part => part.toLowerCase());
  return !parts.some(part => part === "dir" || part === "filter" || part === "grid") || path.toLowerCase().endsWith(".f");
}

export function AdvanceNote({
  projectId,
  projectName,
  root,
  databaseTargets,
  dataCapture,
  activeMenu,
  source,
  onLoadMenu,
  onCopy,
  onReveal,
  onDirtyChange,
  onStatus,
}: {
  projectId: string;
  projectName: string;
  root: string;
  databaseTargets: { App?: string; Sys?: string };
  dataCapture?: AdvanceNoteDataCapture;
  activeMenu?: AdvanceNoteMenu;
  source?: SourceGroup;
  onLoadMenu: (menu: AdvanceNoteMenu) => void;
  onCopy: (text: string) => void;
  onReveal: (path: string) => void;
  onDirtyChange: (dirty: boolean) => void;
  onStatus: (message: string) => void;
}) {
  const [items, setItems] = useState<AdvanceNoteItem[]>([]);
  const [selectedId, setSelectedId] = useState("");
  const [query, setQuery] = useState("");
  const [sourceQuery, setSourceQuery] = useState("");
  const [appliedSourceQuery, setAppliedSourceQuery] = useState("");
  const [projectFiles, setProjectFiles] = useState<string[]>([]);
  const [pendingFiles, setPendingFiles] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [generating, setGenerating] = useState(false);
  const [generated, setGenerated] = useState<{ projectId: string; itemId: string; result: UpdateResult }>();
  const [error, setError] = useState("");
  const [database, setDatabase] = useState("App");
  const saved = useRef("[]");
  const handledCapture = useRef(0);

  useEffect(() => {
    let current = true;
    setLoading(true);
    setError("");
    Promise.all([
      request<AdvanceNoteDocument>("advanceNote.load", { projectId }),
      request<string[]>("workspace.files", { path: root }),
    ]).then(([document, files]) => {
      if (!current) return;
      saved.current = JSON.stringify(document.items);
      setItems(document.items);
      setSelectedId(document.items[0]?.id ?? "");
      setProjectFiles(files);
      onDirtyChange(false);
    }).catch(reason => current && setError((reason as Error).message))
      .finally(() => current && setLoading(false));
    return () => { current = false; };
  }, [projectId, root]);

  useEffect(() => onDirtyChange(JSON.stringify(items) !== saved.current), [items]);

  useEffect(() => {
    if (!dataCapture || loading || handledCapture.current === dataCapture.sequence) return;
    handledCapture.current = dataCapture.sequence;
    if (!selectedId || !items.some(item => item.id === selectedId)) {
      onStatus("Hãy tạo hoặc chọn một yêu cầu Generate Update trước khi nhấn Ctrl+D.");
      return;
    }
    const condition = dataCapture.where.trim() || "1=1";
    const entry = `${dataCapture.table}: ${condition}`;
    setItems(current => current.map(item => {
      if (item.id !== selectedId) return item;
      const previous = item.sql?.[dataCapture.database] ?? emptySql();
      const tableKey = dataCapture.table.toLowerCase();
      const lines = previous.data.split(/\r?\n/).map(line => line.trim()).filter(Boolean);
      const index = lines.findIndex(line => {
        const separator = line.indexOf(":");
        return separator > 0 && line.slice(0, separator).trim().toLowerCase() === tableKey;
      });
      if (index >= 0) lines[index] = entry; else lines.push(entry);
      return {
        ...item,
        sql: { ...item.sql, [dataCapture.database]: { ...previous, data: lines.join("\n") } },
        updatedAt: new Date().toISOString(),
      };
    }));
    setDatabase(dataCapture.database);
    onStatus(`Đã lưu SQL Data ${dataCapture.database} · ${entry}`);
  }, [dataCapture?.sequence, loading, selectedId, items.length]);

  const selected = items.find(item => item.id === selectedId);
  const filtered = useMemo(() => {
    const needle = query.trim().toLowerCase();
    return needle.length === 0 ? items : items.filter(item =>
      [item.title, item.menuCaption, item.sysId].some(value => value.toLowerCase().includes(needle)));
  }, [items, query]);

  function update(changes: Partial<AdvanceNoteItem>) {
    setItems(current => current.map(item => item.id === selectedId
      ? { ...item, ...changes, updatedAt: new Date().toISOString() } : item));
  }

  function add() {
    const item: AdvanceNoteItem = {
      id: crypto.randomUUID(), title: "Yêu cầu mới", status: "Mới", details: "",
      menuId: "", menuCaption: "", sysId: "", link: "", sourceFiles: [],
      updatedAt: new Date().toISOString(),
    };
    setItems(current => [item, ...current]);
    setSelectedId(item.id);
    setPendingFiles([]);
  }

  function remove() {
    if (!selected || !window.confirm(`Xóa yêu cầu “${selected.title}”?`)) return;
    const remaining = items.filter(item => item.id !== selected.id);
    setItems(remaining);
    setSelectedId(remaining[0]?.id ?? "");
    setPendingFiles([]);
  }

  async function save() {
    if (saving) return false;
    setSaving(true);
    setError("");
    try {
      const document = await request<AdvanceNoteDocument>("advanceNote.save", { projectId, items });
      saved.current = JSON.stringify(document.items);
      setItems(document.items);
      onDirtyChange(false);
      onStatus(`Đã lưu ${document.items.length} yêu cầu Generate Update`);
      return true;
    } catch (reason) {
      setError((reason as Error).message);
      return false;
    } finally {
      setSaving(false);
    }
  }

  async function generateUpdate() {
    if (!selected || generating) return;
    setGenerating(true);
    setGenerated(undefined);
    setError("");
    try {
      if (!await save()) return;
      const current = items.find(item => item.id === selected.id) ?? selected;
      const result = await request<UpdateResult>("advanceNote.generateUpdate", {
        projectId, root, item: current,
        appTargetId: databaseTargets.App ?? "",
        sysTargetId: databaseTargets.Sys ?? "",
      });
      onStatus(`Đã Generate Update: ${result.fileCount} file source, ${result.scriptFiles.length} script SQL`);
      setGenerated({ projectId, itemId: selected.id, result });
    } catch (reason) {
      setError((reason as Error).message);
    } finally {
      setGenerating(false);
    }
  }

  useEffect(() => {
    if (!activeMenu || !selected) return;
    update({ menuId: activeMenu.id, menuCaption: activeMenu.caption, sysId: activeMenu.sysId, link: activeMenu.link });
    setSourceQuery("");
    setAppliedSourceQuery("");
    setPendingFiles([]);
  }, [activeMenu?.sequence]);

  const sourceMatches = !!selected && !!source &&
    ((selected.sysId && selected.sysId.toLowerCase() === source.sysId?.toLowerCase()) ||
      (selected.link && selected.link.toLowerCase() === source.link.toLowerCase()));
  const sourceFiles = sourceMatches ? source!.files : [];
  const sourceNeedle = key(appliedSourceQuery.trim());
  const visibleFiles = useMemo(() => {
    const candidates = sourceNeedle ? projectFiles : sourceFiles;
    return candidates.filter(isAllowedSource).filter(path => !sourceNeedle || key(path).includes(sourceNeedle));
  }, [projectFiles, sourceFiles, sourceNeedle]);
  const visibleKeys = useMemo(() => new Set(visibleFiles.map(key)), [visibleFiles]);
  const visibleSelected = pendingFiles.filter(path => visibleKeys.has(key(path))).length;
  const allVisibleSelected = visibleKeys.size > 0 && visibleSelected === visibleKeys.size;

  function addPendingFiles() {
    if (!selected || pendingFiles.length === 0) return;
    const merged = new Map(selected.sourceFiles.map(path => [key(path), path]));
    pendingFiles.forEach(path => merged.set(key(path), path));
    update({ sourceFiles: [...merged.values()] });
    setPendingFiles([]);
  }

  const sql = selected?.sql?.[database] ?? emptySql();
  function updateSql(changes: Partial<AdvanceNoteSql>) {
    update({ sql: { ...selected?.sql, [database]: { ...sql, ...changes } } });
  }

  return <section className="advance-note" aria-label="Generate Update"
    onKeyDown={event => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "s") {
        event.preventDefault();
        void save();
      }
    }}>
    <aside className="advance-note-list">
      <section className="advance-note-request-pane">
        <div className="advance-note-list-header">
          <div><strong>Yêu cầu</strong><small>{projectName}</small></div>
          <Button onClick={add}><Plus size={14} />Thêm</Button>
        </div>
        <input type="search" aria-label="Tìm yêu cầu" placeholder="Tìm tiêu đề hoặc chức năng…"
          value={query} onChange={event => setQuery(event.target.value)} />
        <div className="advance-note-items">
          {loading ? <div className="advance-note-empty">Đang tải yêu cầu…</div> :
            filtered.length === 0 ? <div className="advance-note-empty">{items.length ? "Không tìm thấy yêu cầu." : "Chưa có yêu cầu. Nhấn Thêm để bắt đầu."}</div> :
              filtered.map(item => <button key={item.id} type="button" aria-current={item.id === selectedId ? "page" : undefined}
                onClick={() => { setSelectedId(item.id); setPendingFiles([]); }}>
                <span className="advance-note-item-title">{item.title}</span>
                <span className="advance-note-item-meta">
                  <span>{item.menuCaption || "Chưa liên kết chức năng"}</span>
                  <span>{item.sourceFiles.length} file</span>
                </span>
              </button>)}
        </div>
      </section>

      <section className="advance-note-source" aria-label="Source ERP">
        <div className="advance-note-source-header">
          <div><strong>Source ERP</strong><small>{selected?.menuCaption || "Nhấn đúp menu ERP để nạp cây file"}</small></div>
          {selected?.link && !sourceMatches && <Button variant="ghost" onClick={() => onLoadMenu({
            id: selected.menuId, caption: selected.menuCaption, sysId: selected.sysId, link: selected.link,
          })}>Tải cây</Button>}
          <label className="advance-note-select-all" title="Chọn tất cả file trong cây hiện tại">
            <input type="checkbox" aria-label="Chọn tất cả file trong cây hiện tại"
              disabled={!selected || !visibleKeys.size} checked={allVisibleSelected}
              ref={input => { if (input) input.indeterminate = visibleSelected > 0 && !allVisibleSelected; }}
              onChange={() => setPendingFiles(current => {
                const outside = current.filter(path => !visibleKeys.has(key(path)));
                return allVisibleSelected ? outside : [...outside, ...new Map(visibleFiles.map(path => [key(path), path])).values()];
              })} />
          </label>
        </div>
        <form className="advance-note-source-search" onSubmit={event => {
          event.preventDefault();
          setAppliedSourceQuery(sourceQuery);
        }}>
          <input type="search" aria-label="Tìm file source" placeholder="Tìm file hoặc thư mục trong dự án…"
            value={sourceQuery} onChange={event => setSourceQuery(event.target.value)} />
          <Button type="submit">Tìm</Button>
        </form>
        <div className="advance-note-source-tree">
          <ControllerExplorer root={root} files={visibleFiles} activePath={null}
            emptyMessage={sourceNeedle ? "Không tìm thấy file phù hợp." : "Nhấn đúp một menu ERP để hiện cây source."}
            selectedFiles={pendingFiles} onToggleFile={(path, checked) => setPendingFiles(current => checked
              ? [...current.filter(file => key(file) !== key(path)), path]
              : current.filter(file => key(file) !== key(path)))}
            onCopy={onCopy} onReveal={onReveal} />
        </div>
        <div className="advance-note-source-actions">
          <span>{pendingFiles.length ? `${pendingFiles.length} file đã chọn` : "Chọn file trong cây để thêm"}</span>
          <Button variant="primary" disabled={!selected || pendingFiles.length === 0} onClick={addPendingFiles}>
            <Plus size={14} />Thêm vào File Path
          </Button>
        </div>
      </section>
    </aside>

    <div className="advance-note-detail">
      {!selected ? <div className="advance-note-welcome"><FileText size={28} /><p>Chọn một yêu cầu hoặc tạo yêu cầu mới.</p></div> : <>
        <div className="advance-note-toolbar">
          <div><h2>{selected.title || "Yêu cầu chưa đặt tên"}</h2><small>Cập nhật {new Date(selected.updatedAt).toLocaleString("vi-VN")}</small></div>
          <div className="advance-note-actions">
            <Button variant="destructive" onClick={remove}><Trash2 size={14} />Xóa</Button>
            <Button disabled={generating || saving} onClick={() => void generateUpdate()}>
              <PackageOpen size={14} />{generating ? "Đang tạo…" : "Generate Update"}
            </Button>
            <Button variant="primary" disabled={saving} onClick={() => void save()}><Save size={14} />{saving ? "Đang lưu…" : "Lưu"}</Button>
          </div>
        </div>
        {error && <div className="advance-note-error" role="alert">{error}</div>}
        {generated?.projectId === projectId && generated.itemId === selected.id &&
          <section className="advance-note-title" aria-label="Kết quả Generate Update">
            <span role="status">Đã tạo gói update · {generated.result.fileCount} file source · {generated.result.scriptFiles.length} script SQL</span>
            <input aria-label="Đường dẫn gói update" readOnly value={generated.result.outputPath}
              onFocus={event => event.currentTarget.select()} />
            <div className="advance-note-actions" style={{ justifyContent: "flex-start", flexWrap: "wrap" }}>
              <Button onClick={() => onCopy(generated.result.outputPath)}>Sao chép đường dẫn</Button>
              <Button onClick={() => onReveal(generated.result.outputPath)}>Mở thư mục</Button>
            </div>
          </section>}
        <label className="advance-note-title"><span>Tiêu đề <b>*</b></span>
          <input value={selected.title} onChange={event => update({ title: event.target.value })} />
        </label>
        <label className="advance-note-file-path"><span>File Path</span>
          <textarea value={selected.sourceFiles.join("\n")} onChange={event =>
            update({ sourceFiles: event.target.value.split(/\r?\n/).map(path => path.trim()).filter(Boolean) })} />
        </label>
        <section className="advance-note-sql">
          <div className="advance-note-db-tabs" role="tablist" aria-label="SQL Database">
            {["App", "Sys"].map(value => <button key={value} type="button" role="tab"
              aria-selected={database === value} onClick={() => setDatabase(value)}>{value}</button>)}
          </div>
          <div className="advance-note-sql-pair">
            <label><span>SQL Object</span><textarea value={sql.objects}
              placeholder={"dbo.TenBang\ndbo.TenStore\ndbo.TenFunction"}
              title="Mỗi dòng một table, store hoặc function. Generate Update sẽ sinh DROP và CREATE."
              onChange={event => updateSql({ objects: event.target.value })} /></label>
            <label><span>SQL Data</span><textarea value={sql.data}
              placeholder="zhrdmloaivp: 1=1"
              title="Mỗi dòng theo dạng tên bảng: điều kiện WHERE. Có thể lưu nhanh từ Cấu trúc và dữ liệu bằng Ctrl+D."
              onChange={event => updateSql({ data: event.target.value })} /></label>
          </div>
          <label className="advance-note-top-script"><span>SQL Top Script</span><textarea value={sql.topScript}
            onChange={event => updateSql({ topScript: event.target.value })} /></label>
        </section>
      </>}
    </div>
  </section>;
}
