import { useEffect, useMemo, useRef, useState } from "react";
import { request, operation } from "../bridge";
import { Button, DataGrid, Modal, Select } from "../ui";
import { useTask } from "./useTask";
import { SourceCollectionTree } from "./SourceCollectionTree";
import type { SourceMatch } from "./SourceCollectionTree";

type Version = { group: string; version: string; roots: string[] };
type CatalogEntry = { group: string; version: string; root: string };
const sourceGroups = ["FAP", "FastFE", "FAW", "FBFE", "FBO-FBI", "FE_TT78", "HDDV", "QLTB", "HRM"];
const versionCollator = new Intl.Collator("vi", { numeric: true, sensitivity: "base" });
function orderedVersions(versions: Version[]) {
  return versions.filter(item => !item.version.trimStart().toLowerCase().startsWith("zzz")).sort((left, right) => {
    const leftOther = left.group.toLowerCase() === "khác";
    const rightOther = right.group.toLowerCase() === "khác";
    if (leftOther !== rightOther) return leftOther ? 1 : -1;
    return versionCollator.compare(left.group, right.group) || versionCollator.compare(right.version, left.version);
  });
}
type SearchResult = { files: SourceMatch[]; truncated: boolean; errors: string[] };
type ContentResult = { indices: number[]; skipped: number };
type CopyResult = { copied: number; skipped: number; errors: string[] };

export function SourceCollectionDialog({ samples, destination, onClose }: {
  samples: string[]; destination: string; onClose: () => void;
}) {
  const task = useTask();
  const activeSearch = useRef<ReturnType<typeof operation<SearchResult>>>();
  const [searching, setSearching] = useState(false);
  const [defaultVersion, setDefaultVersion] = useState("");
  const [versions, setVersions] = useState<Version[]>([]);
  const [version, setVersion] = useState("");
  const [showCatalog, setShowCatalog] = useState(false);
  const [catalog, setCatalog] = useState<CatalogEntry[]>();
  const [catalogFilter, setCatalogFilter] = useState("");
  const [fileFilter, setFileFilter] = useState("");
  const [content, setContent] = useState<ContentResult>();
  const [group, setGroup] = useState(sourceGroups[0]);
  const [syncMessage, setSyncMessage] = useState("");
  const [target, setTarget] = useState(destination);
  const [result, setResult] = useState<SearchResult>();
  const [selected, setSelected] = useState<boolean[]>([]);
  const [copy, setCopy] = useState<CopyResult>();
  const selectedCount = selected.filter(Boolean).length;
  const visibleCatalog = catalog?.filter(entry =>
    `${entry.version} ${entry.root}`.toLowerCase().includes(catalogFilter.trim().toLowerCase()));
  const visibleIndices = useMemo(() => {
    if (content) return content.indices;
    const term = fileFilter.trim().toLowerCase().replaceAll("/", "\\");
    return result?.files.flatMap((file, index) =>
      (file.root + "\\" + file.relativePath).toLowerCase().replaceAll("/", "\\").includes(term) ? [index] : []) ?? [];
  }, [result, fileFilter, content]);
  const allVisibleSelected = visibleIndices.length > 0 && visibleIndices.every(index => selected[index]);
  const roots = versions.find(item => item.version === version)?.roots ?? [];

  useEffect(() => {
    let active = true;
    void task.run(async () => {
      const [choices, saved] = await Promise.all([
        request<Version[]>("sourceCollection.versions"),
        request<string>("sourceCollection.default.read"),
      ]);
      if (!active) return;
      const ordered = orderedVersions(choices);
      const preferred = ordered.find(item => item.version.toLowerCase() === saved.toLowerCase());
      setVersions(ordered);
      setDefaultVersion(preferred?.version ?? "");
      setVersion(preferred?.version ?? ordered[0]?.version ?? "");
      if (preferred && samples.length) await runSearch(preferred.roots);
    });
    return () => { active = false; };
  }, [samples]);

  function saveDefaultVersion() {
    if (!version) return;
    const next = version === defaultVersion ? "" : version;
    void task.run(async () => setDefaultVersion(await request<string>("sourceCollection.default.save", { version: next })));
  }

  function resetResults() {
    setResult(undefined);
    setFileFilter("");
    setContent(undefined);
    setSelected([]);
    setCopy(undefined);
    task.clearError();
  }

  async function loadVersions() {
    const choices = orderedVersions(await request<Version[]>("sourceCollection.versions"));
    setVersions(choices);
    setVersion(previous => choices.some(item => item.version === previous) ? previous : choices[0]?.version ?? "");
  }

  function toggleCatalog() {
    setShowCatalog(!showCatalog);
    task.clearError();
    if (!showCatalog) void task.run(async () => setCatalog(await request<CatalogEntry[]>("sourceCollection.catalog")));
  }

  function syncCatalog() {
    setSyncMessage("");
    void task.run(async () => {
      const entries = await operation<CatalogEntry[]>("sourceCollection.sync", { group }, 180_000).promise;
      setCatalog(entries);
      resetResults();
      setSyncMessage(`Đã lưu ${entries.filter(entry => entry.group === group).length} phiên bản ${group}.`);
      await loadVersions();
    });
  }

  function search() {
    if (task.busy || !roots.length) return;
    resetResults();
    void task.run(() => runSearch(roots));
  }

  async function runSearch(searchRoots: string[]) {
    const current = operation<SearchResult>("sourceCollection.find", { roots: searchRoots, samples }, 180_000);
    activeSearch.current = current;
    setSearching(true);
    try {
      const found = await current.promise;
      setResult(found);
      setSelected(found.files.map(file => file.checked));
    } finally {
      activeSearch.current = undefined;
      setSearching(false);
    }
  }

  function copyFiles() {
    setCopy(undefined);
    void task.run(async () => {
      setCopy(await operation<CopyResult>("sourceCollection.copy", {
        destination: target.trim(), files: result?.files.filter((_, i) => selected[i]) ?? [],
      }, 180_000).promise);
    });
  }

  function searchContent() {
    if (!result || !fileFilter.trim() || task.busy) return;
    void task.run(async () => setContent(await operation<ContentResult>("sourceCollection.content", {
      files: result.files, query: fileFilter.trim(),
    }, 180_000).promise));
  }

  async function chooseFolder(onChoose: (path: string) => void) {
    await task.run(async () => {
      const path = await request<string | null>("workspace.folder");
      if (path) onChoose(path);
    });
  }

  return <Modal title="Source Collection" className="source-collection-modal" onClose={() => { if (!task.busy) onClose(); }} wide>
    <div className="source-collection" aria-busy={task.busy}>
      <div className="source-collection-controls">
        {versions.length > 0 ? <Select floating treeGroups popupMinWidth={420}
          label="Phiên bản" searchOptions="Tìm phiên bản" value={version} disabled={task.busy}
          onChange={value => { setVersion(value); resetResults(); }}
          options={versions.map(item => ({ value: item.version, label: item.version, group: item.group }))} />
          : !task.busy && !task.error && <p>Chưa có phiên bản nguồn. Bạn có thể đồng bộ danh mục bên dưới.</p>}
        <Button type="button" disabled={task.busy || !roots.length} onClick={search}>Tìm</Button>
        {version && <Button disabled={task.busy} onClick={saveDefaultVersion}>
          {version === defaultVersion ? "Bỏ mặc định" : "Đặt làm mặc định"}
        </Button>}
        <Button type="button" aria-expanded={showCatalog} disabled={task.busy} onClick={toggleCatalog}>Đồng bộ phiên bản</Button>
        {searching && <Button type="button" onClick={() => void activeSearch.current?.cancel().catch(() => {})}>Dừng tìm</Button>}
      </div>
      {task.busy && <p role="status">Đang xử lý…</p>}
      {task.error && <p className="form-error" role="alert">{task.error}</p>}
      {showCatalog && <section className="source-version-catalog" aria-label="Phiên bản nguồn">
        <div className="source-collection-controls">
          <Select floating label="Nhóm nguồn" value={group} onChange={value => { setGroup(value); setSyncMessage(""); task.clearError(); }}
            options={sourceGroups.map(value => ({ value, label: value }))} disabled={task.busy} />
          <Button variant="primary" disabled={task.busy} onClick={syncCatalog}>Đồng bộ</Button>
          {!!catalog?.length && <input type="search" aria-label="Lọc danh mục nguồn" placeholder="Tìm phiên bản hoặc đường dẫn"
            value={catalogFilter} onChange={event => setCatalogFilter(event.target.value)} />}
        </div>
        {syncMessage && <p role="status">{syncMessage}</p>}
        {visibleCatalog && (visibleCatalog.length > 0 ? <DataGrid table={{
          columns: ["Phiên bản", "Đường dẫn"],
          rows: visibleCatalog.map(entry => [entry.version, entry.root]),
        }} /> : <p>{catalog?.length ? "Không có phiên bản khớp bộ lọc." : "Chưa có phiên bản nào được đồng bộ."}</p>)}
      </section>}
      {!!result?.errors.length && <ul className="source-collection-errors" aria-label="Lỗi đọc nguồn">
        {result.errors.map(error => <li key={error}>{error}</li>)}
      </ul>}
      {result && (result.files.length ? <>
        <div className="source-collection-controls">
          <input type="search" aria-label="Lọc kết quả file" placeholder="Tên file, đường dẫn hoặc nội dung"
            disabled={task.busy} value={fileFilter} onChange={event => { setFileFilter(event.target.value); setContent(undefined); }} />
          <Button disabled={task.busy || !fileFilter.trim()} onClick={searchContent}>Tìm trong file</Button>
          <span role="status">Đã chọn {selectedCount}/{result.files.length} file</span>
          <Button disabled={task.busy || !visibleIndices.length} onClick={() => {
            const visible = new Set(visibleIndices);
            setSelected(previous => previous.map((checked, i) => visible.has(i) ? !allVisibleSelected : checked));
          }}>
            {allVisibleSelected ? "Bỏ chọn kết quả hiện" : "Chọn kết quả hiện"}
          </Button>
        </div>
        {result.truncated && <p>Đang hiển thị 500 file. Chọn nguồn hẹp hơn để xem các kết quả khác.</p>}
        {content && <p role="status">Tìm thấy nội dung trong {content.indices.length} file{content.skipped ? `, bỏ qua ${content.skipped} file quá lớn hoặc không đọc được` : ""}.</p>}
        {visibleIndices.length ? <SourceCollectionTree files={result.files} indices={visibleIndices}
          selected={selected} disabled={task.busy}
          onCheck={(index, checked) => setSelected(previous => previous.map((value, i) => i === index ? checked : value))} />
          : <p role="status">Không có file khớp bộ lọc.</p>}
        <div className="source-collection-path">
          <label>Thư mục đích<input value={target} disabled={task.busy} onChange={event => { setTarget(event.target.value); setCopy(undefined); }} /></label>
          <Button disabled={task.busy} onClick={() => void chooseFolder(path => { setTarget(path); setCopy(undefined); })}>Chọn thư mục</Button>
        </div>
        <div className="form-actions">
          <Button variant="primary" disabled={task.busy || !selectedCount || !target.trim()} onClick={copyFiles}>Sao chép {selectedCount} file</Button>
        </div>
      </> : <p role="status">Không tìm thấy file cùng tên trong nguồn đã chọn.</p>)}
      {copy && <div role="status">
        <p>Đã sao chép {copy.copied} file, bỏ qua {copy.skipped} file đã có, {copy.errors.length} lỗi.</p>
        {copy.errors.length > 0 && <ul className="source-collection-errors">{copy.errors.map((error, i) => <li key={i}>{error}</li>)}</ul>}
      </div>}
    </div>
  </Modal>;
}
