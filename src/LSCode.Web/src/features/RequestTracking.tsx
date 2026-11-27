import { useEffect, useMemo, useRef, useState } from "react";
import { Download, Upload } from "lucide-react";
import type { Table, Target } from "../bridge";
import { request, writeClipboard } from "../bridge";
import { Button, DataGrid, IconButton, Modal, Select } from "../ui";

const emptyTable: Table = { columns: [], rows: [] };
const rosterKey = "lscode.qlyc.roster";
type Employee = { department: string; code: string; name: string };
const employeeKey = (code: string) => code.trim().toUpperCase();
const periods = [...Array.from({ length: 12 }, (_, index) => ({
  label: `T${index + 1}`, requests: `T${index + 1}`, quantity: `SL T${index + 1}`,
})), { label: "Tổng", requests: "Tổng YC", quantity: "Tổng SL" },
{ label: "TB", requests: "TB YC", quantity: "TB SL" }];

function subtotal(values: string[], decimals: number) {
  // The SQL bridge returns invariant decimal strings. Sum exact scaled units to preserve their precision.
  const total = values.reduce((sum, value) => {
    const match = /^(-?)(\d+)(?:\.(\d+))?$/.exec(value.trim());
    if (!match) return sum;
    const [, sign, whole, fraction = ""] = match;
    return sum + BigInt(sign + whole + fraction.padEnd(decimals, "0").slice(0, decimals));
  }, 0n);
  if (!decimals) return total.toString();
  const sign = total < 0 ? "-" : "";
  const digits = (total < 0 ? -total : total).toString().padStart(decimals + 1, "0");
  return sign + digits.slice(0, -decimals) + "." + digits.slice(-decimals);
}

// Keep reading the local roster saved by earlier TSV builds.
function parseRoster(text: string): Employee[] {
  const lines = text.replace(/^\uFEFF/, "").split(/\r?\n/).filter(line => line.trim());
  if (lines.shift()?.trim().toLowerCase() !== "ma_bp\tma_nv\tten_nv" || !lines.length)
    throw new Error("Cần tệp TSV UTF-8 có tiêu đề ma_bp, ma_nv, ten_nv, phân cách bằng Tab.");
  const seen = new Set<string>();
  return lines.map((line, index) => {
    const parts = line.split("\t").map(value => value.trim());
    const [department, code, name] = parts;
    if (parts.length !== 3 || !department || !code || !name)
      throw new Error(`Dòng ${index + 2} cần đủ mã bộ phận, mã nhân viên và tên.`);
    const key = employeeKey(code);
    if (seen.has(key)) throw new Error(`Mã nhân viên bị trùng: ${code}.`);
    seen.add(key);
    return { department: employeeKey(department), code, name };
  });
}

const rosterHeaders = ["ma_bp", "ma_nv", "ten_nv"];

async function readRosterWorkbook(file: File) {
  const { default: { Workbook } } = await import("exceljs");
  const workbook = new Workbook();
  await workbook.xlsx.load(await file.arrayBuffer());
  const sheet = workbook.worksheets[0];
  if (!sheet || rosterHeaders.some((name, index) => sheet.getCell(1, index + 1).text.trim().toLowerCase() !== name))
    throw new Error("Sheet đầu tiên cần có ba cột ma_bp, ma_nv, ten_nv theo đúng thứ tự.");

  const rows = [rosterHeaders.join("\t")];
  sheet.eachRow((row, number) => {
    if (number === 1) return;
    const values = rosterHeaders.map((_, index) => row.getCell(index + 1).text.trim());
    if (!values.some(Boolean)) return;
    if (values.some(value => /[\t\r\n]/.test(value)))
      throw new Error(`Dòng ${number} chứa ký tự Tab hoặc xuống dòng trong ô.`);
    rows.push(values.join("\t"));
  });
  return parseRoster(rows.join("\n"));
}

async function exportRosterTemplate() {
  const { default: { Workbook } } = await import("exceljs");
  const workbook = new Workbook();
  const sheet = workbook.addWorksheet("Nhan vien");
  sheet.columns = rosterHeaders.map((header, index) => ({
    header, width: index === 2 ? 32 : 18, style: { numFmt: "@" },
  }));
  sheet.getRow(1).font = { bold: true };
  const bytes = new Uint8Array(await workbook.xlsx.writeBuffer());
  const base64 = btoa(Array.from(bytes, byte => String.fromCharCode(byte)).join(""));
  await request("requestTracking.exportTemplate", { base64 });
}

function StatisticsGrid({ table }: { table: Table }) {
  const [roster, setRoster] = useState<Employee[]>(() => {
    try { return parseRoster(localStorage.getItem(rosterKey) ?? ""); }
    catch { return []; }
  });
  const [department, setDepartment] = useState("LTHO");
  const [frozen, setFrozen] = useState("2");
  const fileInput = useRef<HTMLInputElement>(null);
  const scroll = useRef<HTMLDivElement>(null);
  const month = `T${new Date().getMonth() + 1}`;
  const [selection, setSelection] = useState<{ start: number[]; end: number[] }>();
  const [showRequests, setShowRequests] = useState(true);
  const [showQuantity, setShowQuantity] = useState(true);
  const [sort, setSort] = useState<{ column: string; descending: boolean }>();
  const [importError, setImportError] = useState("");
  const [importCount, setImportCount] = useState<number>();
  const [importing, setImporting] = useState(false);
  const [exporting, setExporting] = useState(false);
  useEffect(() => {
    if (importCount === undefined) return;
    const timeout = window.setTimeout(() => setImportCount(undefined), 6000);
    return () => window.clearTimeout(timeout);
  }, [importCount]);
  useEffect(() => {
    const element = scroll.current;
    const target = element?.querySelector("[data-current-month]");
    if (element && target) element.scrollLeft += target.getBoundingClientRect().left
      - element.getBoundingClientRect().left - (frozen === "2" ? 256 : frozen === "1" ? 96 : 0);
  }, [table, month, showRequests, showQuantity, frozen]);
  useEffect(() => setSelection(undefined), [table, roster, department, sort, showRequests, showQuantity]);
  const groups = useMemo(() => {
    const employees = new Map(roster.map(person => [employeeKey(person.code), person]));
    const codeIndex = table.columns.indexOf("Nhân viên");
    const groups = new Map<string, { code: string; name: string; data: typeof table.rows[number] }[]>();
    for (const data of table.rows) {
      const code = data[codeIndex] ?? "";
      const employee = employees.get(employeeKey(code));
      if (!employee?.department) continue;
      const key = employee.department;
      const row = { code, name: employee.name, data };
      const group = groups.get(key);
      if (group) group.push(row);
      else groups.set(key, [row]);
    }
    return groups;
  }, [table, roster]);
  const departments = [...groups.keys()].sort((a, b) => a.localeCompare(b, "vi"));
  const departmentOptions = [...new Set(["LTHO", ...roster.map(person => person.department)])].sort();
  const columns = periods.flatMap(period => {
    const metrics = [
      ...(showRequests ? [{ column: period.requests, label: "Số yêu cầu", quantity: false }] : []),
      ...(showQuantity ? [{ column: period.quantity, label: "Số lượng", quantity: true }] : []),
    ];
    return metrics.length ? [{ ...period, metrics }] : [];
  });
  const visibleGroups = departments.filter(key => !department || key === department);
  const columnCount = 2 + columns.reduce((count, period) => count + period.metrics.length, 0);
  const value = (row: typeof table.rows[number], column: string) => row[table.columns.indexOf(column)] ?? "";
  const withinSelection = (row: number, column: number) => selection && [row, column].every((value, index) =>
    value >= Math.min(selection.start[index], selection.end[index]) &&
    value <= Math.max(selection.start[index], selection.end[index]));
  function cell(row: number, column: number) {
    return {
      "data-cell": `${row}:${column}`,
      "data-selected": withinSelection(row, column) ? "true" : undefined,
      onPointerDown: (event: React.PointerEvent) => {
        if (event.button !== 0) return;
        event.preventDefault();
        scroll.current?.focus({ preventScroll: true });
        setSelection(previous => ({ start: event.shiftKey && previous ? previous.start : [row, column], end: [row, column] }));
      },
      onPointerEnter: (event: React.PointerEvent) => {
        if (event.buttons === 1) setSelection(previous => previous && { ...previous, end: [row, column] });
      },
    };
  }
  function header(column: string, label: string, rowSpan?: number) {
    const direction = sort?.column === column ? sort.descending ? "descending" : "ascending" : undefined;
    return <th key={column} scope="col" rowSpan={rowSpan} aria-sort={direction}
      className={column === "code" || column === "name" ? `request-identity request-${column}` : undefined}>
      <button type="button" aria-label={column === "code" || column === "name" ? label : column + " · " + label}
        onClick={() => setSort(direction === "ascending" ? { column, descending: true }
          : direction === "descending" ? undefined : { column, descending: false })}>
        {label}{direction && <span aria-hidden> {direction === "ascending" ? "↑" : "↓"}</span>}
      </button>
    </th>;
  }
  return <div className="request-statistics">
    <div className="request-statistics-controls">
      <IconButton label="Xuất mẫu Excel" disabled={exporting} onClick={() => {
        setImportError("");
        setExporting(true);
        void exportRosterTemplate().catch(error => setImportError((error as Error).message)).finally(() => setExporting(false));
      }}><Download size={16} /></IconButton>
      <IconButton label="Import Excel" disabled={importing} onClick={() => fileInput.current?.click()}><Upload size={16} /></IconButton>
        <input ref={fileInput} hidden type="file" accept=".xlsx" disabled={importing} onChange={async event => {
          const file = event.currentTarget.files?.[0];
          event.currentTarget.value = "";
          if (!file) return;
          setImportError("");
          setImportCount(undefined);
          setImporting(true);
          try {
            const employees = await readRosterWorkbook(file);
            const byCode = new Map(roster.map(person => [employeeKey(person.code), person]));
            for (const person of employees) byCode.set(employeeKey(person.code), person);
            const merged = [...byCode.values()];
            const rows = merged.map(({ department, code, name }) => [department, code, name].join("\t"));
            localStorage.setItem(rosterKey, [rosterHeaders.join("\t"), ...rows].join("\n"));
            setRoster(merged);
            setDepartment("LTHO");
            setImportCount(employees.length);
          } catch (error) { setImportError((error as Error).message); }
          finally { setImporting(false); }
        }} />
      <Select label="Mã bộ phận" value={department} onChange={setDepartment} floating
        options={[{ value: "", label: "Tất cả bộ phận" }, ...departmentOptions.map(code => ({ value: code, label: code }))]} />
      <Select label="Ghim cột" value={frozen} onChange={setFrozen} floating options={[
        { value: "0", label: "Không ghim cột" }, { value: "1", label: "Ghim mã NV" },
        { value: "2", label: "Ghim mã và tên NV" },
      ]} />
      <label><input type="checkbox" checked={showRequests} onChange={e => {
        setShowRequests(e.target.checked); setSort(undefined);
      }} /> Số yêu cầu</label>
      <label><input type="checkbox" checked={showQuantity} onChange={e => {
        setShowQuantity(e.target.checked); setSort(undefined);
      }} /> Số lượng</label>
      <span className="request-statistics-import-status" role="status">
        {importCount !== undefined && `Đã nhập ${importCount} nhân viên.`}
      </span>
    </div>
    {importError && <p className="request-tracking-error" role="alert">{importError}</p>}
    <div ref={scroll} className="request-statistics-scroll" data-frozen={frozen} tabIndex={0}
      aria-label="Bảng thống kê xử lý yêu cầu" onKeyDown={event => {
        if (event.key === "Escape") setSelection(undefined);
        if (!selection || !(event.ctrlKey || event.metaKey) || event.key.toLowerCase() !== "c") return;
        event.preventDefault();
        const rows = new Map<number, string[]>();
        for (const element of event.currentTarget.querySelectorAll<HTMLElement>('[data-selected="true"][data-cell]')) {
          const row = Number(element.dataset.cell?.split(":")[0]);
          const cells = rows.get(row) ?? [];
          cells.push((element.textContent ?? "").replace(/[\r\n\t]/g, " "));
          rows.set(row, cells);
        }
        void writeClipboard([...rows.values()].map(cells => cells.join("\t")).join("\r\n"))
          .catch(error => setImportError((error as Error).message));
      }}>
      <table>
        <thead>
          <tr>{header("code", "Mã NV", columns.length ? 2 : undefined)}{header("name", "Nhân viên", columns.length ? 2 : undefined)}
            {columns.map(period => <th key={period.label} colSpan={period.metrics.length} scope="colgroup"
              data-current-month={period.label === month ? "" : undefined}>{period.label}</th>)}
          </tr>
          {columns.length > 0 && <tr>{columns.flatMap(period => period.metrics.map(metric => header(metric.column, metric.label)))}</tr>}
        </thead>
        {visibleGroups.map(key => {
          const rows = groups.get(key) ?? [];
          const groupIndex = departments.indexOf(key);
          const offset = departments.slice(0, groupIndex).reduce((sum, name) => sum + (groups.get(name)?.length ?? 0) + 1, 0);
          const metrics = columns.flatMap(period => period.metrics);
          const ordered = sort ? [...rows].sort((left, right) => {
            const column = sort.column;
            const comparison = column === "code" || column === "name"
              ? left[column].localeCompare(right[column], "vi", { numeric: true })
              : Number(value(left.data, column)) - Number(value(right.data, column));
            return sort.descending ? -comparison : comparison;
          }) : rows;
          return <tbody key={key}>
            <tr className="request-department" data-tone={groupIndex % 5}><th scope="rowgroup" colSpan={columnCount}>
              <span>{key} · {rows.length}</span>
            </th></tr>
            {ordered.map((row, index) => <tr key={row.code}>
              <td className="request-identity request-code" {...cell(offset + index, 0)}>{row.code}</td>
              <th className="request-identity request-name" scope="row" {...cell(offset + index, 1)}>{row.name}</th>
              {metrics.map((metric, column) => <td key={metric.column}
                {...cell(offset + index, column + 2)}
                className={metric.quantity ? "request-quantity" : undefined}>{value(row.data, metric.column)}</td>)}
            </tr>)}
            <tr className="request-department-total">
              <th className="request-identity request-code" scope="row" {...cell(offset + rows.length, 0)}>Tổng {key}</th>
              <td className="request-identity request-name" {...cell(offset + rows.length, 1)} />
              {metrics.map((metric, column) => {
                const decimals = metric.column.startsWith("TB ") ? 2 : metric.quantity ? 4 : 0;
                return <td key={metric.column} {...cell(offset + rows.length, column + 2)}>
                  {subtotal(rows.map(row => value(row.data, metric.column)), decimals)}
                </td>;
              })}
            </tr>
          </tbody>;
        })}
      </table>
      {!visibleGroups.length && <div className="empty">{roster.length ? "Bộ phận đã chọn chưa có số liệu." : "Import Excel để gán bộ phận cho nhân viên."}</div>}
    </div>
  </div>;
}
const isoDate = (date: Date) => {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 10);
};

function RequestCategories({ requestId, targetId, legacy }: { requestId: string; targetId: string; legacy: boolean }) {
  const [result, setResult] = useState<Table | Error>();
  useEffect(() => {
    if (!requestId) return;
    let active = true;
    void request<Table>("requestTracking.categories", { targetId, legacy, requestId }).then(
      table => { if (active) setResult(table); },
      error => { if (active) setResult(new Error((error as Error).message)); },
    );
    return () => { active = false; };
  }, [requestId, targetId, legacy]);

  return <section className="request-category" aria-labelledby="request-category-title">
    <div className="request-tracking-heading">
      <strong id="request-category-title">Category</strong>
      {requestId && <span title={requestId}>{requestId}</span>}
    </div>
    {!requestId ? <div className="empty">Chọn một yêu cầu để xem đầu mục.</div>
      : !result ? <div className="empty" role="status">Đang tải đầu mục…</div>
      : result instanceof Error ? <p className="request-tracking-error" role="alert">{result.message}</p>
      : !result.rows.length ? <div className="empty">Chưa có đầu mục.</div>
      : <div className="request-category-scroll scrollbar-clean" role="region" aria-label="Bảng đầu mục" tabIndex={0}>
        <table>
          <thead>
            <tr><th scope="col">Chọn</th><th scope="col">Đầu mục</th><th scope="col">Số lượng</th>
              <th scope="col">Lập trình</th><th scope="col">Tester</th></tr>
          </thead>
          <tbody>{result.rows.map(([code, name, selected, quantity, developer, tester]) => <tr key={code}>
            <td><input type="checkbox" aria-label={name ?? code ?? "Đầu mục"} checked={selected === "1"} disabled /></td>
            <th scope="row">{name || "—"}</th>
            <td>{quantity || "0"}</td>
            <td>{developer || "—"}</td>
            <td>{tester || "—"}</td>
          </tr>)}</tbody>
        </table>
      </div>}
  </section>;
}

export function RequestTracking({ targets, defaultTargetId, onClose }: {
  targets: Target[];
  defaultTargetId?: string;
  onClose: () => void;
}) {
  const today = useMemo(() => new Date(), []);
  const first = useMemo(() => new Date(today.getFullYear(), today.getMonth(), 1), [today]);
  const [ready, setReady] = useState(false);
  const [legacyAvailable, setLegacyAvailable] = useState(false);
  const [source, setSource] = useState(defaultTargetId ?? targets[0]?.id ?? "");
  const [user, setUser] = useState("");
  const userEdited = useRef(false);
  const [from, setFrom] = useState(isoDate(first));
  const [to, setTo] = useState(isoDate(today));
  const [year, setYear] = useState(String(today.getFullYear()));
  const [mode, setMode] = useState<"requests" | "statistics">("requests");
  const [table, setTable] = useState<Table>(emptyTable);
  const [requestRevision, setRequestRevision] = useState(0);
  const [requestId, setRequestId] = useState("");
  const requestIdIndex = table.columns.indexOf("Số yêu cầu");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    let active = true;
    void request<{ legacyAvailable: boolean; defaultUser?: string }>("requestTracking.info")
      .then((info) => {
        if (!active) return;
        setLegacyAvailable(info.legacyAvailable);
        if (info.legacyAvailable) setSource("legacy");
        if (!userEdited.current) setUser(info.defaultUser?.trim() ?? "");
      })
      .catch(() => {})
      .finally(() => { if (active) setReady(true); });
    return () => { active = false; };
  }, []);

  const options = [
    ...(legacyAvailable ? [{ value: "legacy", label: "QLYC" }] : []),
    ...targets.map((target) => ({ value: target.id, label: `${target.database} · ${target.server}` })),
  ];
  const payload = { targetId: source === "legacy" ? "" : source, legacy: source === "legacy" };
  async function loadRequests() {
    setBusy(true); setError("");
    try {
      const requests = await request<Table>("requestTracking.list", { ...payload, user, from, to });
      setTable(requests);
      setRequestId(requests.rows[0]?.[requests.columns.indexOf("Số yêu cầu")]?.trim() ?? "");
      setRequestRevision(previous => previous + 1);
      setMode("requests");
    } catch (exception) {
      setError((exception as Error).message);
    } finally { setBusy(false); }
  }
  async function loadStatistics(selectedYear = Number(year)) {
    setBusy(true); setError("");
    try {
      setTable(await request<Table>("requestTracking.statistics", { ...payload, year: selectedYear }));
      setMode("statistics");
    } catch (exception) {
      setError((exception as Error).message);
    } finally { setBusy(false); }
  }

  return (
    <Modal title="QLYC" onClose={onClose} wide className="request-tracking-modal">
      <div className="request-tracking-body">
        <div className="request-tracking-toolbar">
          {mode === "requests" ? <>
            <Select label="Kết nối QLYC" value={source} options={options} disabled={busy || !ready} onChange={(value) => {
              setSource(value); setTable(emptyTable); setRequestId(""); setError("");
            }} floating searchOptions="Tìm kết nối" />
            <label><span>Người xử lý</span><input value={user} onChange={(event) => {
              userEdited.current = true;
              setUser(event.target.value);
            }} placeholder="Tất cả" /></label>
            <label><span>Từ ngày</span><input type="date" value={from} onChange={(event) => setFrom(event.target.value)} /></label>
            <label><span>Đến ngày</span><input type="date" value={to} onChange={(event) => setTo(event.target.value)} /></label>
            <Button variant="primary" disabled={busy || !ready || !source} onClick={() => void loadRequests()}>{busy ? "Đang tải…" : "Tải dữ liệu"}</Button>
            <Button disabled={busy || !ready || !source} onClick={() => {
              const selectedYear = Number(to.slice(0, 4)) || today.getFullYear();
              setYear(String(selectedYear));
              void loadStatistics(selectedYear);
            }}>View All</Button>
          </> : <>
            <label><span>Năm</span><input className="request-year" type="number" min="2000" max="9999" value={year}
              onChange={(event) => setYear(event.target.value)} /></label>
            <Button variant="primary" disabled={busy || !source} onClick={() => void loadStatistics()}>{busy ? "Đang tải…" : "Xem thống kê"}</Button>
            <Button disabled={busy} onClick={() => { setMode("requests"); setTable(emptyTable); setRequestId(""); setError(""); }}>Quay lại QLYC</Button>
          </>}
        </div>
        <div className="request-tracking-heading">
          <strong>{mode === "statistics" ? "Request Processing Statistics" : "Danh sách yêu cầu"}</strong>
          <span>{table.rows.length ? `${table.rows.length} dòng` : "Chưa có dữ liệu"}</span>
        </div>
        {error && <p className="request-tracking-error" role="alert">{error}</p>}
        {!ready ? <div className="empty" role="status">Đang kiểm tra kết nối QLYC…</div>
          : !options.length ? <div className="empty">Chưa có kết nối QLYC. Hãy kết nối SQL trước.</div>
          : mode === "statistics" ? table.columns.length ? <StatisticsGrid table={table} /> : <div className="empty">Chưa có dữ liệu thống kê.</div>
          : <div className="request-tracking-content">
            {table.columns.length ? <DataGrid table={table} rangeSelection
              onRow={row => setRequestId(row[requestIdIndex]?.trim() ?? "")}
              rowClassName={row => requestId && row[requestIdIndex]?.trim() === requestId ? "request-selected" : undefined} />
              : <div className="empty">{busy ? "Đang tải dữ liệu…" : "Chọn điều kiện rồi tải dữ liệu."}</div>}
            <RequestCategories key={JSON.stringify([source, requestRevision, requestId])} requestId={requestId} {...payload} />
          </div>}
      </div>
    </Modal>
  );
}
