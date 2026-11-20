import { useEffect, useMemo, useState } from "react";
import type { Table, Target } from "../bridge";
import { request } from "../bridge";
import { Button, DataGrid, Modal, Select } from "../ui";

const emptyTable: Table = { columns: [], rows: [] };
const isoDate = (date: Date) => {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 10);
};

export function RequestTracking({ targets, defaultTargetId, onClose }: {
  targets: Target[];
  defaultTargetId?: string;
  onClose: () => void;
}) {
  const today = useMemo(() => new Date(), []);
  const first = useMemo(() => new Date(today.getFullYear(), today.getMonth(), 1), [today]);
  const [legacyAvailable, setLegacyAvailable] = useState(false);
  const [source, setSource] = useState(defaultTargetId ?? targets[0]?.id ?? "");
  const [user, setUser] = useState("");
  const [from, setFrom] = useState(isoDate(first));
  const [to, setTo] = useState(isoDate(today));
  const [year, setYear] = useState(String(today.getFullYear()));
  const [mode, setMode] = useState<"requests" | "statistics">("requests");
  const [table, setTable] = useState<Table>(emptyTable);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    void request<{ legacyAvailable: boolean }>("requestTracking.info")
      .then((info) => {
        setLegacyAvailable(info.legacyAvailable);
        if (info.legacyAvailable) setSource("legacy");
      })
      .catch(() => {});
  }, []);

  const options = [
    ...(legacyAvailable ? [{ value: "legacy", label: "QLYC" }] : []),
    ...targets.map((target) => ({ value: target.id, label: `${target.database} · ${target.server}` })),
  ];
  const payload = { targetId: source === "legacy" ? "" : source, legacy: source === "legacy" };
  async function loadRequests() {
    setBusy(true); setError("");
    try {
      setTable(await request<Table>("requestTracking.list", { ...payload, user, from, to }));
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
          <Select label="Kết nối QLYC" value={source} options={options} onChange={(value) => {
            setSource(value); setTable(emptyTable); setError("");
          }} floating searchOptions="Tìm kết nối" />
          {mode === "requests" ? <>
            <label><span>Người xử lý</span><input value={user} onChange={(event) => setUser(event.target.value)} placeholder="Tất cả" /></label>
            <label><span>Từ ngày</span><input type="date" value={from} onChange={(event) => setFrom(event.target.value)} /></label>
            <label><span>Đến ngày</span><input type="date" value={to} onChange={(event) => setTo(event.target.value)} /></label>
            <Button variant="primary" disabled={busy || !source} onClick={() => void loadRequests()}>{busy ? "Đang tải…" : "Tải dữ liệu"}</Button>
            <Button disabled={busy || !source} onClick={() => {
              const selectedYear = Number(to.slice(0, 4)) || today.getFullYear();
              setYear(String(selectedYear));
              void loadStatistics(selectedYear);
            }}>View All</Button>
          </> : <>
            <label><span>Năm</span><input className="request-year" type="number" min="2000" max="9999" value={year}
              onChange={(event) => setYear(event.target.value)} /></label>
            <Button variant="primary" disabled={busy || !source} onClick={() => void loadStatistics()}>{busy ? "Đang tải…" : "Xem thống kê"}</Button>
            <Button onClick={() => { setMode("requests"); setTable(emptyTable); setError(""); }}>Quay lại QLYC</Button>
          </>}
        </div>
        <div className="request-tracking-heading">
          <strong>{mode === "statistics" ? "Request Processing Statistics" : "Danh sách yêu cầu"}</strong>
          <span>{table.rows.length ? `${table.rows.length} dòng` : "Chưa có dữ liệu"}</span>
        </div>
        {error && <p className="request-tracking-error" role="alert">{error}</p>}
        {!options.length ? <div className="empty">Chưa có kết nối QLYC. Hãy kết nối SQL trước.</div>
          : table.columns.length ? <DataGrid table={table} rangeSelection />
          : <div className="empty">{busy ? "Đang tải dữ liệu…" : "Chọn điều kiện rồi tải dữ liệu."}</div>}
      </div>
    </Modal>
  );
}
