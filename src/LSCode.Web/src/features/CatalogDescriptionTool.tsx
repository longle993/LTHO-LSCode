import { useEffect, useState } from "react";
import { ArrowDown, ArrowUp, Copy, Info, Trash2 } from "lucide-react";
import { request, writeClipboard, type Target } from "../bridge";
import { Button, IconButton, Select, WorkbenchNotice } from "../ui";
import { useTask } from "./useTask";

const sqlTypes = ["nvarchar", "varchar", "nchar", "char", "int", "bigint", "smallint", "tinyint", "numeric", "decimal", "bit", "date", "datetime", "datetime2", "uniqueidentifier"];
const sqlTypeOptions = sqlTypes.map(value => ({ value, label: value === "numeric" ? "numeric · 19,5" : value }));
const descriptionExample = `Danh mục điểm đánh giá theo hệ số
Hiệu lực từ kỳ/năm: kiểu số, khoá chính
Mã điểm: kiểu chuỗi, khoá chính
Tên điểm: kiểu chuỗi, bắt buộc nhập
Tên khác: kiểu chuỗi
Hệ số đánh giá (%): kiểu số thập phân
Trạng thái: 1-Còn sử dụng, 0-Không còn sử dụng`;
const descriptionPrompt = `Chuyển yêu cầu nghiệp vụ bên dưới thành mô tả danh mục để dán vào LSCode.

Chỉ trả về văn bản thuần, không dùng Markdown, dấu đầu dòng, đánh số, tiêu đề phụ hoặc lời giải thích.
Dòng đầu là tên danh mục. Mỗi dòng tiếp theo có dạng: Tên trường: kiểu, ràng buộc.
Dùng các cụm "kiểu chuỗi", "kiểu số", "kiểu số nguyên", "kiểu số thập phân" hoặc "kiểu tiền". Nếu có ràng buộc, ghi "khóa chính", "bắt buộc nhập" hoặc "mặc định <giá trị>" theo yêu cầu. Không tự thêm trường hay ràng buộc.
Giữ tên trường nghiệp vụ bằng tiếng Việt. Nếu có trường kỳ/năm kết hợp, giữ trên một dòng để LSCode tự tách. Nếu trạng thái dùng 1/0 như ví dụ, ghi đúng dòng Trạng thái trong mẫu.
LSCode tự quy đổi mã thành char(16), chuỗi khác thành nvarchar(128), số nguyên thành int, thập phân/tiền thành numeric(19,5) và trạng thái thành char(1). Không ghi các kiểu SQL vào kết quả.

Ví dụ định dạng đầu ra:
${descriptionExample}

Yêu cầu nghiệp vụ:
[Dán nội dung yêu cầu tại đây]`;

type Field = { name: string; type: string; size: string; key: boolean; required: boolean; default: string | null; origin: string };
type MenuNode = { entry: { id: string; caption: string }; children: MenuNode[] };
type Preview = { fingerprint: string; sql: string; files: { path: string; text: string }[] };
const normalize = (value: string) => value.toLowerCase().normalize("NFD").replace(/\p{M}/gu, "").replaceAll("đ", "d");
const identifier = (value: string) => normalize(value).replace(/\([^)]*\)/g, "").replace(/[^a-z0-9]+/g, "_").replace(/^_+|_+$/g, "").slice(0, 50);
const sizeFor = (type: string, code = false) => type === "char" ? (code ? "16" : "1")
  : type === "nvarchar" ? "128" : type === "varchar" || type === "nchar" ? (code ? "16" : "128")
    : type === "numeric" || type === "decimal" ? "19,5" : type === "datetime2" ? "7" : "";
const field = (origin = "", name = "", type = "nvarchar", size = sizeFor(type), key = false,
  value: string | null = null, required = key): Field => ({ origin, name, type, size, key, required, default: value });

export function parseCatalogDescription(value: string) {
  const lines = value.split(/\r?\n/).map(line => line.trim().replace(/^[•▪*\-]+\s*|^[o○]\s+/u, "")).filter(Boolean);
  if (!lines.length) return { title: "", fields: [] as Field[] };
  const title = lines[0].replace(/^thêm mới màn hình\s+/iu, "").trim();
  const proposed = lines.slice(1).flatMap(line => {
    if (/^(mo ta|man hinh|nhap lieu)/.test(normalize(line))) return [];
    const separator = line.indexOf(":");
    const sourceLabel = (separator < 0 ? line : line.slice(0, separator)).trim();
    const sourceRules = separator < 0 ? "" : line.slice(separator + 1).trim();
    const label = normalize(sourceLabel);
    const rules = normalize(sourceRules);
    const key = /khoa chinh|primary key/.test(rules);
    const required = key || /bat buoc(?: nhap)?|not null/.test(rules);

    if (/ky\s*\/\s*nam/.test(label)) {
      return [field(line, "ky_hl", "int", "", key, null, required), field(line, "nam_hl", "int", "", key, null, required)];
    }
    if (label === "trang thai") return [field(line, "status", "char", "1", key, "1", true)];

    const code = label.startsWith("ma ") || (/\bma\b/.test(label) && /kieu chuoi/.test(rules));
    const decimal = /kieu so thap phan|kieu tien|tien te|decimal|numeric/.test(rules) || /he so|%|so tien|don gia|thanh tien/.test(label);
    const integer = /kieu so(?:\s|,|$)|so nguyen|integer|\bint\b/.test(rules);
    let type = code ? "char" : decimal ? "numeric" : integer ? "int" : "nvarchar";
    if (/kieu chuoi|chuoi ky tu|string/.test(rules)) type = code ? "char" : "nvarchar";
    const defaultMatch = sourceRules.match(/mặc định(?:\s+là|\s*=)?\s*([^,;]+)/iu)
      ?? sourceRules.match(/mac dinh(?:\s+la|\s*=)?\s*([^,;]+)/iu);
    return [field(line, identifier(sourceLabel), type, sizeFor(type, code), key, defaultMatch?.[1]?.trim() ?? null, required)];
  });
  const descriptiveName = proposed.find(entry => /^ten_/.test(entry.name) && normalize(entry.origin.split(":")[0]) !== "ten khac")?.name ?? "ten";
  return {
    title,
    fields: proposed.map(item => normalize(item.origin.split(":")[0]) === "ten khac" ? { ...item, name: descriptiveName + "2" } : item),
  };
}

export function CatalogDescriptionTool({ root, template, app, sys, onBusy, onDirty, onOpen }: {
  root: string; template: string; app?: Target; sys?: Target;
  onBusy: (busy: boolean) => void; onDirty: (dirty: boolean) => void; onOpen: (path: string) => void;
}) {
  const task = useTask();
  const [parents, setParents] = useState<{ value: string; label: string }[]>();
  const [description, setDescription] = useState("");
  const [fields, setFields] = useState<Field[]>([]);
  const [configuration, setConfiguration] = useState({ title: "", titleEn: "", table: "", controller: "", template, parent: "", mapping: "" });
  const [review, setReview] = useState<{ preview: Preview; design: { menu: Record<string, string | null> } }>();
  const [file, setFile] = useState("");
  const [created, setCreated] = useState<string[]>([]);
  const [notice, setNotice] = useState("");
  useEffect(() => onBusy(task.busy), [task.busy, onBusy]);
  useEffect(() => onDirty(Boolean(description || fields.length) && !created.length), [description, fields.length, created.length, onDirty]);

  function clearResult() {
    setReview(undefined);
    setCreated([]);
    setNotice("");
    task.clearError();
  }

  function parse() {
    void task.run(async () => {
      const parsed = parseCatalogDescription(description);
      if (!parsed.title) return;
      const stem = identifier(parsed.title.replace(/^danh mục\s+/iu, ""));
      setFields(parsed.fields);
      setConfiguration(old => ({ ...old, title: parsed.title, table: "zdm_" + stem, controller: "zdm_" + stem }));
      setReview(undefined);
      setCreated([]);
      setNotice(`Đã tách ${parsed.fields.length} trường. Hãy duyệt lại trước khi xem trước.`);
    });
  }

  function preview() {
    if (!app || !sys) return;
    void task.run(async () => {
      clearResult();
      const c = Object.fromEntries(Object.entries(configuration).map(([key, value]) => [key, value.trim()])) as typeof configuration;
      const source = await request<{ tables: string[] }>("catalogSource.discover", { root, template: c.template });
      if (source.tables.length !== 1) throw new Error("Chọn mẫu có đúng một bảng.");
      const original = source.tables[0].replace(/[\[\]]/g, "");
      const tables = { [source.tables[0]]: original.includes(".") ? "dbo." + c.table : c.table };
      const mappings = c.mapping.split(/\r?\n/).filter(line => line.trim()).map(line => {
        const parts = line.split("=").map(value => value.trim());
        if (parts.length !== 2 || !parts.every(Boolean)) throw new Error("Nhập ánh xạ theo dạng cột_cũ = cột_mới.");
        return parts;
      });
      const menu = await request<{ values: Record<string, string | null> }>("project.menu.new", { targetId: sys.id, selectedId: c.parent });
      Object.assign(menu.values, { bar: c.title, bar2: c.titleEn, sysid: c.controller, link: c.controller + ".aspx", ...(c.parent ? { wmenu_id0: c.parent } : {}) });
      const design = { source: { root, template: c.template, controller: c.controller, titleVi: c.title, titleEn: c.titleEn,
        tables, fields: Object.fromEntries(mappings) }, table: c.table, fields, menu: menu.values };
      const result = await request<Preview>("catalogDescription.preview", { design, targetId: sys.id, app: { targetId: app.id } });
      setFile("sql");
      setReview({ preview: result, design });
      setNotice("Đã tạo bản xem trước SQL, menu và source ERP.");
    });
  }

  function apply() {
    if (!review || !app || !sys) return;
    void task.run(async () => {
      const files = await request<string[]>("catalogDescription.apply", {
        design: review.design, fingerprint: review.preview.fingerprint, targetId: sys.id, app: { targetId: app.id },
      });
      setCreated(files);
      setReview(undefined);
      setNotice(`Đã tạo bảng, menu ERP và ${files.length} file source.`);
    });
  }

  if (!root || !app || !sys) return <p className="catalog-source-empty">Mở dự án ERP có kết nối App và Sys để tạo danh mục.</p>;
  return <div className="catalog-source catalog-description" aria-busy={task.busy}>
    <div className="catalog-source-scroll">
      <fieldset disabled={task.busy} className="catalog-source-body catalog-description-body">
        <section className="catalog-source-inputs">
          <div className="catalog-description-heading">
            <div><h3>Tạo thiết kế từ yêu cầu</h3><p className="catalog-source-meta">Dòng đầu là tên danh mục/bảng; mỗi dòng sau khai báo một trường.</p></div>
            <Button onClick={() => void task.run(async () => { await writeClipboard(descriptionPrompt); setNotice("Đã sao chép prompt cho AI."); })}><Copy size={14} />Sao chép prompt cho AI</Button>
          </div>
          <details className="catalog-description-guide">
            <summary><Info size={15} />Prompt chuẩn hóa yêu cầu bằng AI</summary>
            <p>Sao chép prompt, thay dòng cuối bằng yêu cầu nghiệp vụ rồi gửi cho AI. Dán kết quả vào ô Mô tả danh mục và chọn Tách trường.</p>
            <pre style={{ whiteSpace: "pre-wrap" }}>{descriptionPrompt}</pre>
          </details>
          <label className="catalog-description-text">Mô tả danh mục<textarea rows={7} value={description}
            placeholder={descriptionExample} onChange={event => { setDescription(event.target.value); clearResult(); }} /></label>
          <Button className="catalog-description-parse" variant="primary" onClick={parse} disabled={!description.trim()}>Tách trường</Button>
          {fields.length > 0 && ([
            ["title", "Tên tiếng Việt"], ["titleEn", "Tên tiếng Anh"], ["table", "Bảng dbo"], ["controller", "Controller"],
            ["template", "Mẫu ASPX ERP"], ["mapping", "Ánh xạ cột mẫu"],
          ] as const).map(([key, label]) => <label key={key} className={key === "mapping" ? "catalog-description-mapping" : undefined}>{label}
            {key === "mapping" ? <textarea rows={3} placeholder="ma_cu = ma_moi" value={configuration[key]}
              onChange={event => { setConfiguration({ ...configuration, [key]: event.target.value }); clearResult(); }} />
              : <input value={configuration[key]} onChange={event => { setConfiguration({ ...configuration, [key]: event.target.value }); clearResult(); }} />}
          </label>)}
          {fields.length > 0 && <>
            <Button onClick={() => void task.run(async () => {
              const tree = await request<MenuNode[]>("project.commands", { targetId: sys.id, appDatabase: app.database });
              function options(nodes: MenuNode[], depth = 0): { value: string; label: string }[] {
                return nodes.filter(node => node.children.length > 0).flatMap(node => [
                  { value: node.entry.id, label: "— ".repeat(depth) + node.entry.caption + " · " + node.entry.id },
                  ...options(node.children, depth + 1),
                ]);
              }
              setParents([{ value: "", label: "Menu gốc" }, ...options(tree)]);
            })}>Chọn menu cha từ ERP</Button>
            {parents && <Select label="Menu cha" value={configuration.parent} options={parents} floating searchOptions="Tìm menu cha"
              disabled={task.busy} onChange={parent => { setConfiguration({ ...configuration, parent }); clearResult(); }} />}
          </>}
        </section>
        <section className="catalog-source-preview catalog-description-fields">
          <div className="catalog-description-heading"><div><h3>Duyệt trường trước khi tạo</h3>
            <p className="catalog-source-meta">Có thể sửa mọi đề xuất. Bảng giữ chiều cao cố định và cuộn độc lập.</p></div>
            <Button onClick={() => { setFields([...fields, field()]); clearResult(); }}>Thêm trường</Button></div>
          <div className="catalog-description-scroll" tabIndex={0} role="region" aria-label="Bảng trường danh mục">
            <table className="catalog-description-table">
              <thead><tr>{["Tên cột", "Kiểu SQL", "Kích thước", "Mặc định", "Khóa chính", "Bắt buộc", "Mô tả gốc", "Thao tác"].map(label =>
                <th key={label} scope="col">{label}</th>)}</tr></thead>
              <tbody>{fields.map((item, index) => {
                function update(changes: Partial<Field>) {
                  setFields(previous => previous.map((entry, i) => i === index ? { ...entry, ...changes } : entry));
                  clearResult();
                }
                return <tr key={index}>
                  <td><input aria-label={`Tên cột ${index + 1}`} value={item.name} onChange={event => update({ name: event.target.value })} /></td>
                  <td><Select label={`Kiểu SQL ${index + 1}`} value={item.type} options={sqlTypeOptions} floating
                    onChange={type => update({ type, size: sizeFor(type, item.name.startsWith("ma_")) })} /></td>
                  <td><input aria-label={`Kích thước ${index + 1}`} value={item.size} onChange={event => update({ size: event.target.value })} /></td>
                  <td><input aria-label={`Mặc định ${index + 1}`} value={item.default ?? ""} onChange={event => update({ default: event.target.value || null })} /></td>
                  <td><input aria-label={`Khóa chính ${index + 1}`} type="checkbox" checked={item.key}
                    onChange={event => update({ key: event.target.checked, required: event.target.checked || item.required })} /></td>
                  <td><input aria-label={`Bắt buộc ${index + 1}`} type="checkbox" checked={item.required} onChange={event => update({ required: event.target.checked })} /></td>
                  <td><span className="catalog-description-origin" title={item.origin}>{item.origin}</span></td>
                  <td className="catalog-description-actions">
                    <IconButton label={`Đưa trường ${item.name || index + 1} lên`} disabled={index === 0} onClick={() => {
                      const reordered = [...fields]; [reordered[index - 1], reordered[index]] = [item, reordered[index - 1]];
                      setFields(reordered); clearResult();
                    }}><ArrowUp size={14} /></IconButton>
                    <IconButton label={`Đưa trường ${item.name || index + 1} xuống`} disabled={index === fields.length - 1} onClick={() => {
                      const reordered = [...fields]; [reordered[index + 1], reordered[index]] = [item, reordered[index + 1]];
                      setFields(reordered); clearResult();
                    }}><ArrowDown size={14} /></IconButton>
                    <IconButton label={`Bỏ trường ${item.name || index + 1}`} onClick={() => { setFields(fields.filter((_, i) => i !== index)); clearResult(); }}><Trash2 size={14} /></IconButton>
                  </td>
                </tr>;
              })}</tbody>
            </table>
          </div>
        </section>
      </fieldset>
      {review && <section className="catalog-source-preview catalog-description-review">
        <p>App {app.database} · Sys {sys.database} · {root}</p>
        <Select label="Nội dung xem trước" value={file} onChange={setFile} floating searchOptions="Tìm file xem trước"
          options={[{ value: "sql", label: "SQL" }, { value: "menu", label: "Menu ERP" },
            ...review.preview.files.map(entry => ({ value: entry.path, label: entry.path }))]} />
        <pre>{file === "sql" ? review.preview.sql : file === "menu" ? ["bar", "wmenu_id", "wmenu_id0", "menu_id", "link", "sysid"].map(key => key + " = " + (review.design.menu[key] ?? "")).join("\n")
          : review.preview.files.find(entry => entry.path === file)?.text}</pre>
      </section>}
      {created.length > 0 && <section className="catalog-description-result" role="status">
        <strong>Danh mục đã được tạo</strong><span>{created.length} file source đã được ghi.</span>
        <div>{created.map(path => <Button key={path} onClick={() => onOpen(path)}>{path.split(/[\\/]/).pop()}</Button>)}</div>
      </section>}
    </div>
    <div className="modal-footer catalog-description-footer">
      {task.busy && <span role="status" className="catalog-source-meta">Đang xử lý…</span>}
      <Button disabled={task.busy || !fields.length || !configuration.title.trim() || !configuration.template.trim()} onClick={preview}>Xem trước</Button>
      <Button variant="primary" disabled={task.busy || !review} onClick={apply}>Tạo danh mục</Button>
    </div>
    <WorkbenchNotice error={task.error} status={notice} onClose={() => { task.clearError(); setNotice(""); }} />
  </div>;
}
