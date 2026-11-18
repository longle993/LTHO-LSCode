import { Folder, FolderOpen, X } from "lucide-react";
import { FileIcon } from "../ControllerExplorer";
import { useEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Button } from "../ui";

export function ProjectHistory({ root, name, files, activePath, onOpen, onRemove, onCloseProject, onCloseAllProjects }: {
  root: string;
  name: string;
  files: string[];
  activePath?: string | null;
  onOpen: (path: string) => void;
  onRemove: (path: string) => void;
  onCloseProject: () => void;
  onCloseAllProjects: () => void;
}) {
  const [menu, setMenu] = useState<{ x: number; y: number }>();
  const menuRef = useRef<HTMLDivElement>(null);
  const ownerRef = useRef<HTMLElement>(null);
  const dismiss = () => { setMenu(undefined); ownerRef.current?.focus(); };
  useEffect(() => {
    if (!menu) return;
    const outside = (event: PointerEvent) => {
      if (!menuRef.current?.contains(event.target as Node)) setMenu(undefined);
    };
    menuRef.current?.querySelector("button")?.focus();
    document.addEventListener("pointerdown", outside);
    return () => document.removeEventListener("pointerdown", outside);
  }, [menu]);
  const folders = useMemo(() => {
    const groups = new Map<string, string[]>();
    const projectRoot = root.replaceAll("\\", "/").replace(/\/$/, "").toLowerCase();
    for (const path of files) {
      const normalized = path.replaceAll("\\", "/");
      const folder = normalized.slice(0, normalized.lastIndexOf("/"));
      const group = folder.toLowerCase() === projectRoot ? "" : folder;
      const siblings = groups.get(group);
      if (siblings) siblings.push(path);
      else groups.set(group, [path]);
    }
    return groups;
  }, [files, root]);
  const nameOf = (path: string) => path.split(/[\\/]/).at(-1) ?? path;
  const rows = (paths: string[]) => paths.map((path) => (
    <div className="history-file" key={path}>
      <button className="tree-row" title={path}
        aria-current={path.toLowerCase() === activePath?.toLowerCase() ? "page" : undefined}
        onClick={() => onOpen(path)}>
        <FileIcon path={path} /><span>{nameOf(path)}</span>
      </button>
      <button className="history-remove" type="button" title="Xóa khỏi lịch sử"
        aria-label={`Xóa ${nameOf(path)} khỏi lịch sử`} onClick={() => onRemove(path)}>
        <X size={14} />
      </button>
    </div>
  ));
  return <><details className="history-group" open>
    <summary ref={ownerRef} className="tree-row folder-row" title={root} onContextMenu={event => {
      event.preventDefault();
      setMenu({ x: Math.max(8, Math.min(event.clientX, innerWidth - 248)), y: Math.max(8, Math.min(event.clientY, innerHeight - 100)) });
    }}><FolderOpen size={15} /><span>{name}</span></summary>
    <div className="tree-children">
      {[...folders].map(([folder, paths]) => folder ? (
        <details className="history-group" key={folder} open>
          <summary className="tree-row folder-row" title={folder}><Folder size={15} /><span>{nameOf(folder)}</span></summary>
          <div className="tree-children">{rows(paths)}</div>
        </details>
      ) : <div key={folder}>{rows(paths)}</div>)}
    </div>
  </details>{menu && createPortal(<div ref={menuRef} role="menu" aria-label="Đóng file theo dự án"
    className="table-action-menu explorer-action-menu" style={{ left: menu.x, top: menu.y }} onKeyDown={event => {
      if (event.key === "Escape") { event.preventDefault(); dismiss(); }
      if (event.key === "Tab") setMenu(undefined);
      if (event.key === "ArrowDown" || event.key === "ArrowUp") {
        event.preventDefault();
        const items = Array.from(menuRef.current?.querySelectorAll("button") ?? []);
        const index = items.indexOf(document.activeElement as HTMLButtonElement);
        items[(index + (event.key === "ArrowDown" ? 1 : -1) + items.length) % items.length]?.focus();
      }
    }}>
    <Button variant="ghost" role="menuitem" onClick={() => { dismiss(); onCloseProject(); }}>Close project</Button>
    <Button variant="ghost" role="menuitem" onClick={() => { dismiss(); onCloseAllProjects(); }}>Close all projects</Button>
  </div>, document.body)}</>;
}
