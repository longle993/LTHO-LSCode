import { useEffect, useMemo, useRef, useState } from "react";
import { FileText, FolderSearch2, Plus, Save, Trash2 } from "lucide-react";
import { request } from "../bridge";
import ControllerExplorer from "../ControllerExplorer";
import { Button, Select } from "../ui";

export type AdvanceNoteMenu = {
  id: string;
  caption: string;
  sysId: string;
  link: string;
};

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
  sql?: Record<string, { objects: string; data: string; topScript: string }>;
};

type AdvanceNoteDocument = { projectId: string; items: AdvanceNoteItem[]; filePath: string };
type SourceGroup = { link: string; sysId?: string; files: string[] };

const statuses = ["Mới", "Đang xử lý", "Chờ phản hồi", "Hoàn thành"];

export function AdvanceNote({
  projectId,
  projectName,
  root,
  activeMenu,
  source,
  onChooseMenu,
  onLoadMenu,
  onOpen,
  onCopy,
  onReveal,
  onDirtyChange,
  onStatus,
}: {
  projectId: string;
  projectName: string;
  root: string;
  activeMenu?: AdvanceNoteMenu;
  source?: SourceGroup;
  onChooseMenu: () => void;
  onLoadMenu: (menu: AdvanceNoteMenu) => void;
  onOpen: (path: string) => void;
  onCopy: (text: string) => void;
  onReveal: (path: string) => void;
  onDirtyChange: (dirty: boolean) => void;
  onStatus: (message: string) => void;
}) {
  const [items, setItems] = useState<AdvanceNoteItem[]>([]);
  const [selectedId, setSelectedId] = useState("");
  const [query, setQuery] = useState("");
  const [filePath, setFilePath] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [waitingForMenu, setWaitingForMenu] = useState(false);
  const saved = useRef("[]");
  const [database, setDatabase] = useState("App");

  useEffect(() => {
    let current = true;
    setLoading(true);
    setError("");
    request<AdvanceNoteDocument>("advanceNote.load", { projectId })
      .then((document) => {
        if (!current) return;
        saved.current = JSON.stringify(document.items);
        setItems(document.items);
        setSelectedId(document.items[0]?.id ?? "");
        setFilePath(document.filePath);
        onDirtyChange(false);
      })
      .catch((reason) => current && setError((reason as Error).message))
      .finally(() => current && setLoading(false));
    return () => { current = false; };
  }, [projectId]);

  useEffect(() => onDirtyChange(JSON.stringify(items) !== saved.current), [items]);

  const selected = items.find((item) => item.id === selectedId);
  const filtered = useMemo(() => {
    const needle = query.trim().toLowerCase();
    return needle.length === 0 ? items : items.filter((item) =>
      [item.title, item.status, item.menuCaption, item.sysId].some(value => value.toLowerCase().includes(needle)));
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
  }

  function remove() {
    if (!selected || !window.confirm(`Xóa yêu cầu “${selected.title}”?`)) return;
    const remaining = items.filter(item => item.id !== selected.id);
    setItems(remaining);
    setSelectedId(remaining[0]?.id ?? "");
  }

  async function save() {
    if (saving) return;
    setSaving(true);
    setError("");
    try {
      const document = await request<AdvanceNoteDocument>("advanceNote.save", { projectId, items });
      saved.current = JSON.stringify(document.items);
      setItems(document.items);
      setFilePath(document.filePath);
      onDirtyChange(false);
      onStatus(`Đã lưu ${document.items.length} yêu cầu Advance Note`);
    } catch (reason) {
      setError((reason as Error).message);
    } finally {
      setSaving(false);
    }
  }

  useEffect(() => {
    if (!waitingForMenu || !activeMenu || !selected) return;
    update({
      menuId: activeMenu.id,
      menuCaption: activeMenu.caption,
      sysId: activeMenu.sysId,
      link: activeMenu.link,
    });
    setWaitingForMenu(false);
    onLoadMenu(activeMenu);
  }, [activeMenu?.id, waitingForMenu]);

  const sourceMatches = !!selected && !!source &&
    ((selected.sysId && selected.sysId.toLowerCase() === source.sysId?.toLowerCase()) ||
      (selected.link && selected.link.toLowerCase() === source.link.toLowerCase()));
  const visibleFiles = sourceMatches ? source!.files : selected?.sourceFiles ?? [];

  const sql = selected?.sql?.[database] ?? { objects: "", data: "", topScript: "" };
  function updateSql(changes: Partial<typeof sql>) {
    update({ sql: { ...selected?.sql, [database]: { ...sql, ...changes } } });
  }

  return <section className="advance-note" aria-label="Advance Note"
    onKeyDown={(event) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "s") {
        event.preventDefault();
        void save();
      }
    }}>
    <aside className="advance-note-list">
      <div className="advance-note-list-header">
        <div><strong>Yêu cầu</strong><small>{projectName}</small></div>
        <Button onClick={add}><Plus size={14} />Thêm</Button>
      </div>
      <input type="search" aria-label="Tìm yêu cầu" placeholder="Tìm tiêu đề, trạng thái, chức năng…"
        value={query} onChange={(event) => setQuery(event.target.value)} />
      <div className="advance-note-items">
        {loading ? <div className="advance-note-empty">Đang tải yêu cầu…</div> :
          filtered.length === 0 ? <div className="advance-note-empty">{items.length ? "Không tìm thấy yêu cầu." : "Chưa có yêu cầu. Nhấn Thêm để bắt đầu."}</div> :
          filtered.map(item => <button key={item.id} type="button" aria-current={item.id === selectedId ? "page" : undefined}
            onClick={() => setSelectedId(item.id)}>
            <span className="advance-note-item-title">{item.title}</span>
            <span className="advance-note-item-meta"><span>{item.status}</span><span>{item.menuCaption || "Chưa chọn chức năng"}</span></span>
          </button>)}
      </div>
      {filePath && <small className="advance-note-file" title={filePath}>XML · {filePath}</small>}
    </aside>

    <div className="advance-note-detail">
      {!selected ? <div className="advance-note-welcome"><FileText size={28} /><p>Chọn một yêu cầu hoặc tạo yêu cầu mới.</p></div> : <>
        <div className="advance-note-toolbar">
          <div><h2>{selected.title || "Yêu cầu chưa đặt tên"}</h2><small>Cập nhật {new Date(selected.updatedAt).toLocaleString("vi-VN")}</small></div>
          <div className="advance-note-actions">
            <Button className="danger" onClick={remove}><Trash2 size={14} />Xóa</Button>
            <Button variant="primary" disabled={saving} onClick={() => void save()}><Save size={14} />{saving ? "Đang lưu…" : "Lưu"}</Button>
          </div>
        </div>
        {error && <div className="advance-note-error" role="alert">{error}</div>}
        <div className="advance-note-fields">
          <label><span>Tiêu đề <b>*</b></span><input value={selected.title} onChange={event => update({ title: event.target.value })} /></label>
          <label><span>Trạng thái</span><Select label="Trạng thái" value={selected.status}
            options={statuses.map(value => ({ value, label: value }))} onChange={status => update({ status })} /></label>
        </div>
        <div className="advance-note-workspace">
          <label className="advance-note-editor"><span>File Path</span>
            <textarea value={selected.sourceFiles.join("\n")} onChange={event =>
              update({ sourceFiles: event.target.value.split(/\r?\n/).filter(path => path.trim().length > 0) })} />
          </label>
          <label className="advance-note-editor"><span>Chi tiết kỹ thuật</span>
            <textarea value={selected.details} placeholder="Ghi nội dung cần xử lý, kiểm tra và kết quả…"
              onChange={event => update({ details: event.target.value })} /></label>
          <section className="advance-note-source">
            <div className="advance-note-source-header">
              <div><strong>Source ERP</strong><small>{selected.menuCaption || "Chưa liên kết chức năng"}{selected.sysId ? ` · ${selected.sysId}` : ""}</small></div>
              <Button onClick={() => { setWaitingForMenu(true); onChooseMenu(); }}><FolderSearch2 size={14} />{waitingForMenu ? "Chọn bên trái…" : "Chọn chức năng"}</Button>
            </div>
            {selected.link || selected.sysId ? <>
              {!sourceMatches && <Button variant="ghost" onClick={() => onLoadMenu({ id: selected.menuId, caption: selected.menuCaption,
                sysId: selected.sysId, link: selected.link })}>Tải lại cây source</Button>}
              <ControllerExplorer root={root} files={visibleFiles} activePath={null} emptyMessage="Chức năng chưa tìm thấy source."
                selectedFiles={selected.sourceFiles} onToggleFile={(path, checked) => update({
                  sourceFiles: checked ? [...selected.sourceFiles.filter(file => file.toLowerCase() !== path.toLowerCase()), path]
                    : selected.sourceFiles.filter(file => file.toLowerCase() !== path.toLowerCase()),
                })}
                onOpen={onOpen} onCopy={onCopy} onReveal={onReveal} />
            </> : <div className="advance-note-empty">Nhấn “Chọn chức năng” rồi chọn một mục trong cây Chức năng ERP.</div>}
          </section>
          <section className="advance-note-sql">
            <Select label="SQL Database" value={database} options={["App", "Sys"].map(value => ({ value, label: value }))}
              onChange={setDatabase} />
            <label>SQL Object<textarea value={sql.objects} onChange={event => updateSql({ objects: event.target.value })} /></label>
            <label>SQL Data<textarea value={sql.data} onChange={event => updateSql({ data: event.target.value })} /></label>
            <label className="advance-note-top-script">SQL Top Script<textarea value={sql.topScript}
              onChange={event => updateSql({ topScript: event.target.value })} /></label>
          </section>
        </div>
      </>}
    </div>
  </section>;
}
