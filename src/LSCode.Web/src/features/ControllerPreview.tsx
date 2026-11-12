import { useEffect, useState } from "react";
import { request } from "../bridge";
import { Modal, Select } from "../ui";
import { xmlIncludeBuffers } from "../editor";

type Column = { name: string; label: string; width: number };
type DirCell = { name: string; label: string; isLabel: boolean; column: number; span: number };
type DirLayout = { widths: number[]; rows: DirCell[][] };
type Preview = {
  kind: "grid" | "dir";
  title: string;
  subtitle: string;
  toolbar: (string | null)[];
  views: { name: string; columns: Column[]; layout: DirLayout | null }[];
};

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
              {layout ? <div className="grid-preview-scroll" tabIndex={0} role="region" aria-label="Bố cục ô Dir">
                <div className="dir-preview-layout">
                  {layout.rows.map((row, index) => <div key={index} className="dir-preview-row"
                    style={{ gridTemplateColumns: layout.widths.map((width) => width + "px").join(" ") }}>
                    {row.map((cell) => <div key={cell.column} className={cell.isLabel ? "dir-preview-label" : "dir-preview-field"}
                      style={{ gridColumn: cell.column + " / span " + cell.span }} title={cell.label}>
                      {cell.isLabel ? cell.label : <input type="text" aria-label={cell.label} readOnly />}
                    </div>)}
                  </div>)}
                </div>
              </div> : columns.length ? <div className="grid-preview-scroll" tabIndex={0} role="region" aria-label="Bố cục cột Grid">
                <table aria-label={preview.title || "Bố cục Grid"} style={{ width: columns.reduce((sum, column) => sum + column.width, 0) }}>
                  <colgroup>{columns.map((column) => <col key={column.name} style={{ width: column.width }} />)}</colgroup>
                  <thead><tr>{columns.map((column) => <th key={column.name} scope="col"
                    title={column.label + " · " + column.width + " px"}>{column.label}</th>)}</tr></thead>
                  <tbody>
                    <tr><td colSpan={columns.length}>Bản xem trước bố cục</td></tr>
                    {[0, 1, 2].map((row) => <tr key={row} aria-hidden="true">
                      {columns.map((column) => <td key={column.name} />)}
                    </tr>)}
                  </tbody>
                </table>
              </div> : <p className="grid-preview-empty">View chưa có cột hiển thị.</p>}
            </div>
          </>}
      </div>
    </Modal>
  );
}
