import { useEffect, useId, useState } from "react";
import { request } from "../bridge";
import { Modal, Select } from "../ui";
import { xmlIncludeBuffers } from "../editor";

type Column = { name: string; label: string; width: number };
type DirCell = { name: string; label: string; isLabel: boolean; column: number; span: number;
  description?: string | null; detail?: { controller: string; columns: Column[]; error?: string | null } | null };
type DirRows = { widths: number[]; rows: DirCell[][] };
type DirLayout = DirRows & { categories?: (DirRows & { label: string })[] };
type Preview = {
  kind: "grid" | "dir";
  title: string;
  subtitle: string;
  toolbar: (string | null)[];
  views: { name: string; columns: Column[]; layout: DirLayout | null }[];
};

function PreviewTable({ columns, label }: { columns: Column[]; label: string }) {
  return <table aria-label={label} style={{ width: columns.reduce((sum, column) => sum + column.width, 0) }}>
    <colgroup>{columns.map((column) => <col key={column.name} style={{ width: column.width }} />)}</colgroup>
    <thead><tr>{columns.map((column) => <th key={column.name} scope="col"
      title={column.label + " · " + column.width + " px"}>{column.label}</th>)}</tr></thead>
    <tbody>
      <tr><td colSpan={columns.length}>Bản xem trước bố cục</td></tr>
      {[0, 1, 2].map((row) => <tr key={row} aria-hidden="true">
        {columns.map((column) => <td key={column.name} />)}
      </tr>)}
    </tbody>
  </table>;
}

function PreviewDirRows({ widths, rows }: DirRows) {
  const tracks = widths.map((width) => width + "px").join(" ");
  return rows.map((row, index) => <div key={index} className="dir-preview-row" style={{ gridTemplateColumns: tracks }}>
    {row.map((cell) => <div key={cell.column} className={cell.isLabel ? "dir-preview-label" : "dir-preview-field"}
      style={{ gridColumn: cell.column + " / span " + cell.span }} title={cell.description ?? cell.label}>
      {cell.description != null ? <span className="dir-preview-description">{cell.description}</span> :
        cell.isLabel ? cell.label : cell.detail ? <div className="dir-preview-detail">
          <strong>{cell.label}</strong>
          {cell.detail.columns.length ? <div className="grid-preview-scroll" tabIndex={0}
            role="region" aria-label={cell.label}><PreviewTable columns={cell.detail.columns} label={cell.label} /></div> :
            <p className="grid-preview-empty">{cell.detail.error || "Grid chưa có cột hiển thị."}</p>}
        </div> : <input type="text" aria-label={cell.label} readOnly />}
    </div>)}
  </div>);
}

function PreviewDirLayout({ layout }: { layout: DirLayout }) {
  const [selected, setSelected] = useState(0);
  const id = useId();
  const categories = layout.categories ?? [];
  const active = categories[selected];
  return <>
    {layout.rows.length > 0 && <div className="grid-preview-scroll" tabIndex={0} role="region" aria-label="Bố cục ô Dir">
      <div className="dir-preview-layout">
        <PreviewDirRows widths={layout.widths} rows={layout.rows} />
      </div>
    </div>}
    {categories.length > 0 && <div className="dir-preview-tabs" role="tablist" aria-label="Nhóm Dir">
      {categories.map((tab, index) => <button type="button" role="tab" key={index} id={id + "-tab-" + index}
        aria-controls={id + "-panel"} aria-selected={index === selected} tabIndex={index === selected ? 0 : -1}
        onClick={() => setSelected(index)}
        onKeyDown={(event) => {
          const next = { ArrowLeft: (index + categories.length - 1) % categories.length,
            ArrowRight: (index + 1) % categories.length, Home: 0, End: categories.length - 1 }[event.key];
          if (next === undefined) return;
          event.preventDefault();
          setSelected(next);
          event.currentTarget.parentElement?.querySelectorAll("button")[next]?.focus();
        }}>{tab.label}</button>)}
    </div>}
    {active && <div className="grid-preview-scroll" tabIndex={0} id={id + "-panel"}
      role="tabpanel" aria-labelledby={id + "-tab-" + selected}>
      <div className="dir-preview-layout" key={selected}>
        <PreviewDirRows widths={active.widths} rows={active.rows} />
      </div>
    </div>}
  </>;
}

export function ControllerPreview({ text, path, onClose }: { text: string; path: string | null; onClose: () => void }) {
  const [preview, setPreview] = useState<Preview>();
  const [error, setError] = useState("");
  const [selected, setSelected] = useState("");
  useEffect(() => {
    let current = true;
    void request<Preview>("xml.controllerPreview", { text, path, buffers: xmlIncludeBuffers() }).then((result) => {
      if (!current) return;
      const preferred = result.views.findIndex((view) => view.name.toLowerCase() === result.kind);
      setSelected(String(Math.max(0, preferred)));
      setPreview(result);
    }).catch((failure: Error) => { if (current) setError(failure.message); });
    return () => { current = false; };
  }, [text, path]);
  const view = preview?.views[Number(selected)];
  const columns = view?.columns ?? [];
  const layout = view?.layout;
  return (
    <Modal title={preview ? "Xem trước " + (preview.kind === "dir" ? "Dir" : "Grid") : "Xem trước Grid / Dir"} onClose={onClose} wide>
      <div className="tool-body">
        {error ? <p className="form-error" role="alert">{error}</p> : !preview ?
          <p className="muted" role="status">Đang đọc XML…</p> : <>
            {preview.views.length > 1 && <Select label="View" value={selected}
              options={preview.views.map((view, index) => ({ label: view.name, value: String(index) }))}
              onChange={setSelected} />}
            <div className="grid-preview">
              {(preview.title || preview.subtitle) && <header className="grid-preview-heading">
                {preview.title && <h3>{preview.title}</h3>}
                {preview.subtitle && <p>{preview.subtitle}</p>}
              </header>}
              {preview.toolbar.length > 0 && <div className="grid-preview-toolbar" aria-label="Thanh công cụ">
                {preview.toolbar.map((label, index) => label === null ?
                  <span className="grid-preview-separator" key={index} aria-hidden="true" /> :
                  <span key={index}>{label}</span>)}
              </div>}
              {layout ? <PreviewDirLayout key={selected} layout={layout} /> : columns.length ? <div className="grid-preview-scroll" tabIndex={0} role="region" aria-label="Bố cục cột Grid">
                <PreviewTable columns={columns} label={preview.title || "Bố cục Grid"} />
              </div> : <p className="grid-preview-empty">View chưa có cột hiển thị.</p>}
            </div>
          </>}
      </div>
    </Modal>
  );
}
