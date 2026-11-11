import { Folder, FolderOpen, X } from "lucide-react";
import { FileIcon } from "../ControllerExplorer";

export function ProjectHistory({ root, name, files, activePath, onOpen, onRemove }: {
  root: string;
  name: string;
  files: string[];
  activePath?: string | null;
  onOpen: (path: string) => void;
  onRemove: (path: string) => void;
}) {
  const folders = new Map<string, string[]>();
  const projectRoot = root.replaceAll("\\", "/").replace(/\/$/, "").toLowerCase();
  for (const path of files) {
    const normalized = path.replaceAll("\\", "/");
    const folder = normalized.slice(0, normalized.lastIndexOf("/"));
    const group = folder.toLowerCase() === projectRoot ? "" : folder;
    const siblings = folders.get(group);
    if (siblings) siblings.push(path);
    else folders.set(group, [path]);
  }
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
  return <details className="history-group" open>
    <summary className="tree-row folder-row" title={root}><FolderOpen size={15} /><span>{name}</span></summary>
    <div className="tree-children">
      {[...folders].map(([folder, paths]) => folder ? (
        <details className="history-group" key={folder} open>
          <summary className="tree-row folder-row" title={folder}><Folder size={15} /><span>{nameOf(folder)}</span></summary>
          <div className="tree-children">{rows(paths)}</div>
        </details>
      ) : <div key={folder}>{rows(paths)}</div>)}
    </div>
  </details>;
}
