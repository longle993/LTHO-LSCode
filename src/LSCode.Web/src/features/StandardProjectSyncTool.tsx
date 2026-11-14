import { useEffect, useRef, useState } from "react";
import { Button } from "../ui";
import { operation } from "../bridge";
import type { Project } from "../bridge";
import { useTask } from "./useTask";

type Preview = {
  code: string;
  title: string;
  sourcePath: string;
  status: "added" | "updated" | "unchanged" | "skipped";
  note?: string;
};
type Summary = {
  added: number;
  updated: number;
  unchanged: number;
  skipped: number;
  fingerprint: string;
  projects: Preview[];
};
const userStorageKey = "lscode.fsdStudio.currUser";
function rememberedUser() {
  try { return localStorage.getItem(userStorageKey) ?? ""; } catch { return ""; }
}

export function StandardProjectSyncTool({ onProjects }: { onProjects: (projects: Project[]) => void }) {
  const [sourceFile, setSourceFile] = useState("D:\\FAST\\Code\\LTHO\\FSDStudioSource\\FSDStudio\\clsDatabase.cs");
  const [currentUser, setCurrentUser] = useState(rememberedUser);
  const [preview, setPreview] = useState<Summary>();
  const [message, setMessage] = useState("");
  const pending = useRef<{ cancel: () => Promise<unknown> }>();
  const task = useTask();
  const reset = () => { setPreview(undefined); setMessage(""); };
  useEffect(() => () => { void pending.current?.cancel().catch(() => {}); }, []);

  async function read() {
    await task.run(async () => {
      const current = operation<Summary>("project.standard.preview", { sourceFile, currentUser }, 120_000);
      pending.current = current;
      setPreview(await current.promise);
      try { localStorage.setItem(userStorageKey, currentUser.trim()); } catch { /* Storage is optional. */ }
    });
  }
  async function syncNow() {
    await task.run(async () => {
      const requested = operation<{ summary: Summary; projects: Project[] }>("project.standard.sync",
        { sourceFile, currentUser }, 180_000);
      pending.current = requested;
      const result = await requested.promise;
      setPreview(result.summary);
      onProjects(result.projects);
      try { localStorage.setItem(userStorageKey, currentUser.trim()); } catch { /* Storage is optional. */ }
      if (result.summary.added === 0 && result.summary.updated === 0) {
        setMessage("Danh mục đã cập nhật; không có dự án cần ghi.");
        return;
      }
      setMessage(`Đã đồng bộ: ${result.summary.added} dự án mới, ${result.summary.updated} dự án cập nhật.`);
    });
  }
  async function apply() {
    if (!preview) return;
    await task.run(async () => {
      const current = operation<{ summary: Summary; projects: Project[] }>("project.standard.apply",
        { sourceFile, currentUser, fingerprint: preview.fingerprint }, 120_000);
      pending.current = current;
      const result = await current.promise;
      onProjects(result.projects);
      setMessage(`Đã đồng bộ: ${result.summary.added} dự án mới, ${result.summary.updated} dự án cập nhật.`);
    });
  }

  return <section className="standard-project-sync">
    <h3>Đồng bộ toàn bộ từ FSD Studio</h3>
    <p className="muted">Dùng CurrUser của FSD Studio để lấy danh mục chuẩn. LSCode nhớ tên này sau lần nhập đầu tiên. Tài khoản SQL nhúng được đọc tại chỗ từ file nguồn FSD Studio.</p>
    <div className="form-row">
      <label className="field">File clsDatabase.cs của FSD Studio
        <input value={sourceFile} disabled={task.busy} onChange={(event) => { setSourceFile(event.target.value); reset(); }} />
      </label>
      <label className="field">CurrUser trong FSD Studio (nhập một lần)
        <input value={currentUser} disabled={task.busy} onChange={(event) => { setCurrentUser(event.target.value); reset(); }} />
      </label>
    </div>
    <div className="inline-checks">
      <Button variant="primary" disabled={task.busy || !sourceFile.trim() || !currentUser.trim()} onClick={() => void syncNow()}>
        Đồng bộ từ danh mục chuẩn
      </Button>
      <Button disabled={task.busy || !sourceFile.trim() || !currentUser.trim()} onClick={() => void read()}>
        Xem trước
      </Button>
      {task.busy && <Button onClick={() => void pending.current?.cancel()}>Dừng</Button>}
    </div>
    {preview && <>
      <p>{preview.projects.length} dự án: {preview.added} mới · {preview.updated} cập nhật · {preview.unchanged} không đổi · {preview.skipped} bỏ qua</p>
      <div className="standard-project-preview">
        {preview.projects.map((item, index) => <div key={`${item.code}-${index}`} className="standard-project-row">
          <strong>{item.code || "(thiếu mã)"}</strong><span>{item.title}</span>
          <small>{item.status}{item.note ? ` · ${item.note}` : ""}</small>
          <small title={item.sourcePath}>{item.sourcePath}</small>
        </div>)}
      </div>
      <Button variant="primary" disabled={task.busy || preview.projects.length === 0 ||
        (preview.added === 0 && preview.updated === 0)} onClick={() => void apply()}>
        Đồng bộ toàn bộ
      </Button>
    </>}
    {message && <p role="status">{message}</p>}
    <p className="form-error">{task.error}</p>
  </section>;
}
