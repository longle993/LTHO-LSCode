import { useState, useRef, useEffect } from "react";
import { FolderOpen, Plus, Search } from "lucide-react";
import { Button } from "../ui";
import { request, operation } from "../bridge";
import type { Project } from "../bridge";
import type { ToolProps } from "./types";
import { useTask } from "./useTask";
export function SyncTool({ onProjects }: Pick<ToolProps, "onProjects">) {
  const [name, setName] = useState(""),
    [root, setRoot] = useState("\\\\172.168.5.14\\CustomerPro"),
    [matches, setMatches] = useState<
      { name: string; folder: string; configPath: string }[]
    >([]);
  const task = useTask();
  const pending = useRef<{ cancel: () => Promise<unknown> }>(),
    alive = useRef(true);
  useEffect(
    () => () => {
      alive.current = false;
      void pending.current?.cancel().catch(() => {});
    },
    [],
  );
  const discover = () =>
    task.run(async () => {
      const op = operation<typeof matches>("project.discover", { root, name });
      pending.current = op;
      const result = await op.promise;
      if (alive.current) setMatches(result);
    });
  return (
    <div className="tool-body">
      <div className="form-row">
        <label className="field">
          Thư mục CustomerPro
          <input
            disabled={task.busy}
            value={root}
            onChange={(e) => setRoot(e.target.value)}
          />
        </label>
        <label className="field">
          Tên dự án
          <input
            value={name}
            disabled={task.busy}
            onChange={(e) => setName(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter") void discover();
            }}
          />
        </label>
      </div>
      <Button
        variant="primary"
        disabled={task.busy || !name}
        onClick={() => void discover()}
      >
        <Search size={14} />
        Tìm ứng dụng
      </Button>
      {task.busy && (
        <Button onClick={() => void pending.current?.cancel()}>Dừng</Button>
      )}
      <div className="project-list">
        {matches.map((m) => (
          <button
            className="project-row"
            key={m.configPath}
            disabled={task.busy}
            onClick={() =>
              void task.run(async () => {
                const op = operation("project.sync", {
                  name: m.name,
                  configPath: m.configPath,
                });
                pending.current = op;
                await op.promise;
                const projects = await request<Project[]>("project.list");
                if (alive.current) {
                  setMatches([]);
                  onProjects(projects);
                }
              })
            }
          >
            <FolderOpen size={16} />
            <span>{m.folder}</span>
            <Plus size={15} />
          </button>
        ))}
      </div>
      <p className="form-error">{task.error}</p>
    </div>
  );
}
