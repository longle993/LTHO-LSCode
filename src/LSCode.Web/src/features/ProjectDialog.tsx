import { writeClipboard } from "../bridge";
import { useEffect, useState } from "react";
import { ArrowRight, FolderOpen, Trash2 } from "lucide-react";
import { Button, IconButton, Modal } from "../ui";
import { request } from "../bridge";
import type { Project, Settings } from "../bridge";
import { useTask } from "./useTask";
import { LoadingLine } from "../LoadingLine";
import { SyncTool } from "./SyncTool";
export function ProjectDialog({
  projects,
  lastAccess,
  onProjects,
  onOpen,
  onClose,
}: {
  projects: Project[];
  lastAccess?: Record<string, string>;
  onProjects: (p: Project[]) => void;
  onOpen: (p: Project) => Promise<void>;
  onClose: () => void;
}) {
  const [editing, setEditing] = useState<Project>();
  const [deleting, setDeleting] = useState<Project>();
  const [updatePath, setUpdatePath] = useState("");
  const [sharedAccess, setSharedAccess] = useState<Record<string, string>>();
  useEffect(() => {
    let current = true;
    void request<Settings>("settings.read").then((preferences) => {
      if (current) setSharedAccess(preferences?.projectLastAccess);
    }).catch(() => {});
    return () => { current = false; };
  }, []);
  const access = (id: string) => Math.max(Date.parse(lastAccess?.[id] ?? "") || 0, Date.parse(sharedAccess?.[id] ?? "") || 0);
  const [programPath, setProgramPath] = useState("");
  const [metadata, setMetadata] = useState("");
  const [search, setSearch] = useState(""),
    [mode, setMode] = useState("list"),
    [xml, setXml] = useState(""),
    [preview, setPreview] = useState<Project[]>([]),
    [overwrite, setOverwrite] = useState(false);
  const [form, setForm] = useState({
    name: "",
    server: ".",
    database: "",
    sourcePath: "",
  });
  const task = useTask();
  const [opening, setOpening] = useState(false);
  async function open(project: Project) {
    setOpening(true);
    try {
      await onOpen(project);
    } finally {
      setOpening(false);
    }
  }
  const exportProjects = () =>
    task.run(async () => {
      const text = await request<string>("project.export");
      setXml(text);
      setMode("export");
    });
  return (
    <Modal title="Dự án của bạn" onClose={onClose}>
      <div className="tool-body">
        <div className="tool-toolbar">
          <Button
            variant={mode === "list" ? "secondary" : "ghost"}
            onClick={() => setMode("list")}
          >
            Danh sách
          </Button>
          <Button variant="ghost" onClick={() => {
            setEditing(undefined);
            setProgramPath("");
            setUpdatePath("");
            setForm({ name: "", server: ".", database: "", sourcePath: "" });
            setMode("create");
          }}>
            Tạo profile
          </Button>
          <Button variant={mode === "sync" ? "secondary" : "ghost"} onClick={() => setMode("sync")}>
            Đồng bộ dự án
          </Button>
          <Button variant="ghost" onClick={() => setMode("import")}>
            Import XML
          </Button>
          <Button variant="ghost" onClick={() => void exportProjects()}>
            Export
          </Button>
        </div>
        {mode === "sync" && (
          <SyncTool onProjects={(updated) => {
            onProjects(updated);
            setMode("list");
          }} />
        )}
        {mode === "list" && (
          <>
            <input
              aria-label="Tìm dự án"
              placeholder="Tìm theo tên, server hoặc database"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
            <div className="project-list loading-region" aria-busy={opening}>
              <LoadingLine busy={opening} label="Đang mở dự án…" />
              {projects
                .filter((p) =>
                  [p.name, p.server, p.database]
                    .join(" ")
                    .toLowerCase()
                    .includes(search.toLowerCase()),
                )
                .sort((a, b) => access(b.id) - access(a.id) || a.name.localeCompare(b.name, "vi"))
                .map((p) => (
                  <div className="project-list-entry" key={p.id}>
                  <button
                    className="project-row"
                    disabled={task.busy}
                    onContextMenu={(e) => {
                      e.preventDefault();
                      void task.run(async () => {
                        const detail = await request<{
                          programPath: string;
                          updatePath: string;
                          metadata: string;
                        }>("project.details", { id: p.id });
                        setEditing(p);
                        setForm({
                          name: p.name,
                          server: p.server,
                          database: p.database,
                          sourcePath: p.sourcePath,
                        });
                        setProgramPath(detail.programPath);
                        setUpdatePath(detail.updatePath ?? "");
                        setMetadata(detail.metadata);
                        setMode("edit");
                      });
                    }}
                    title="Nhấp để mở · Chuột phải để xem và sửa thông tin"
                    onClick={() => void task.run(() => open(p))}
                  >
                    <FolderOpen size={19} className="project-icon" />
                    <span>
                      <strong>{p.name}</strong>
                      <small>
                        {p.server} / {p.database}
                      </small>
                      <small>{p.sourcePath}</small>
                    </span>
                    <ArrowRight size={16} />
                  </button>
                  <IconButton label={"Xóa profile " + p.name} disabled={task.busy} onClick={() => {
                    setDeleting(p);
                    setMode("delete");
                  }}><Trash2 size={15} /></IconButton>
                  </div>
                ))}
              {projects.length === 0 && (
                <div className="empty">
                  Chưa có dự án. Tạo profile hoặc nhập catalog XML.
                </div>
              )}
            </div>
          </>
        )}
        {mode === "delete" && deleting && <>
          <p>Xóa profile <strong>{deleting.name}</strong> khỏi danh sách? Thư mục dự án và database vẫn được giữ.</p>
          <div className="inline-checks">
            <Button disabled={task.busy} onClick={() => setMode("list")}>Hủy</Button>
            <Button className="table-drop-action" disabled={task.busy} onClick={() => void task.run(async () => {
              await request("project.delete", { id: deleting.id });
              onProjects(await request<Project[]>("project.list"));
              setDeleting(undefined);
              setMode("list");
            })}>Xóa profile</Button>
          </div>
        </>}
        {["create", "edit"].includes(mode) && (
          <form
            className="form"
            onSubmit={(e) => {
              e.preventDefault();
              void task.run(async () => {
                const updated = await request<Project>(
                  mode === "edit" ? "project.update" : "project.create",
                  { ...form, id: editing?.id, programPath, updatePath },
                );
                onProjects(await request<Project[]>("project.list"));
                setMode("list");
                if (mode === "edit") await open(updated);
              });
            }}
          >
            {[
              ["name", "Tên dự án"],
              ["server", "Server"],
              ["database", "Database"],
              ["sourcePath", "Thư mục nguồn"],
            ].map(([key, label]) => (
              <label key={key}>
                {label}
                <input
                  required={key === "name"}
                  value={form[key as keyof typeof form]}
                  onChange={(e) => setForm({ ...form, [key]: e.target.value })}
                />
              </label>
            ))}
            <label>
              Thư mục update
              <input value={updatePath} onChange={(e) => setUpdatePath(e.target.value)}
                placeholder={`\\\\172.168.5.14\\CustomerUpdate\\${form.name || "<Tên dự án>"}\\update\\`} />
            </label>
            {mode === "edit" && (
              <>
                <label>
                  Đường dẫn chương trình
                  <input
                    value={programPath}
                    onChange={(e) => setProgramPath(e.target.value)}
                  />
                </label>
                <details>
                  <summary>Metadata dự án</summary>
                  <p className="muted">
                    Kết nối dự án đã đồng bộ lấy từ web.config. Đồng bộ lại để
                    áp dụng thay đổi kết nối.
                  </p>
                  <pre className="metadata-preview">{metadata}</pre>
                </details>
              </>
            )}
            <Button
              type="button"
              onClick={() =>
                void task.run(async () => {
                  const path = await request<string | null>("workspace.folder");
                  if (path) setForm({ ...form, sourcePath: path });
                })
              }
            >
              Chọn thư mục
            </Button>
            <Button variant="primary" disabled={task.busy}>
              {mode === "edit" ? "Lưu thay đổi" : "Tạo profile"}
            </Button>
          </form>
        )}
        {mode === "import" && (
          <>
            <label className="field">
              Catalog XML
              <textarea
                className="tool-text"
                value={xml}
                onChange={(e) => {
                  setXml(e.target.value);
                  setPreview([]);
                }}
              />
            </label>
            <div className="inline-checks">
              <label>
                <input
                  type="checkbox"
                  checked={overwrite}
                  onChange={(e) => setOverwrite(e.target.checked)}
                />
                Cập nhật dự án trùng
              </label>
              <Button
                onClick={() =>
                  void task.run(async () =>
                    setPreview(
                      await request<Project[]>("project.import.preview", {
                        xml,
                      }),
                    ),
                  )
                }
              >
                Xem trước
              </Button>
            </div>
            {preview.length > 0 && (
              <>
                <p>
                  {preview.length} dự án:{" "}
                  {preview.map((p) => p.name).join(", ")}
                </p>
                <Button
                  variant="primary"
                  disabled={task.busy}
                  onClick={() =>
                    void task.run(async () => {
                      const r = await request<{ projects: Project[] }>(
                        "project.import",
                        { xml, overwrite },
                      );
                      onProjects(r.projects);
                      setMode("list");
                    })
                  }
                >
                  Nhập catalog
                </Button>
              </>
            )}
          </>
        )}
        {mode === "export" && (
          <>
            <p className="muted">
              Các trường nhạy cảm nhận diện theo tên đã để trống. Kiểm tra
              trường tùy biến trước khi chia sẻ.
            </p>
            <textarea
              className="tool-text"
              aria-label="XML export"
              value={xml}
              readOnly
            />
            <Button onClick={() => void writeClipboard(xml)}>
              Sao chép XML
            </Button>
          </>
        )}
        <p className="form-error" role="alert">
          {task.error}
        </p>
      </div>
    </Modal>
  );
}
