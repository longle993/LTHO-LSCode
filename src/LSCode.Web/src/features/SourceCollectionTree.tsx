import { useMemo, useState } from "react";
import { ChevronDown, ChevronRight, Folder, FolderOpen } from "lucide-react";
import { FileIcon } from "../ControllerExplorer";

export type SourceMatch = { root: string; relativePath: string; checked: boolean };
type FolderNode = {
  name: string;
  path: string;
  folders: Map<string, FolderNode>;
  files: { name: string; index: number }[];
};

function buildTree(files: SourceMatch[], indices: number[]) {
  const roots = new Map<string, FolderNode>();
  const folder = (siblings: Map<string, FolderNode>, name: string, path: string) => {
    const key = name.toLowerCase();
    const existing = siblings.get(key);
    if (existing) return existing;
    const node: FolderNode = { name, path, folders: new Map(), files: [] };
    siblings.set(key, node);
    return node;
  };
  for (const index of indices) {
    const { root, relativePath } = files[index];
    let parent = folder(roots, root, root);
    const parts = relativePath.split(/[\\/]/);
    const name = parts.pop() ?? relativePath;
    for (const part of parts) parent = folder(parent.folders, part, parent.path + "\\" + part);
    parent.files.push({ name, index });
  }
  return roots;
}

export function SourceCollectionTree({ files, indices, selected, disabled, onCheck }: {
  files: SourceMatch[];
  indices: number[];
  selected: boolean[];
  disabled: boolean;
  onCheck: (index: number, value: boolean) => void;
}) {
  const roots = useMemo(() => buildTree(files, indices), [files, indices]);
  const [collapsed, setCollapsed] = useState<Set<string>>(new Set());

  function renderFolder(node: FolderNode) {
    const open = !collapsed.has(node.path);
    const Chevron = open ? ChevronDown : ChevronRight;
    const Icon = open ? FolderOpen : Folder;
    return <li key={node.path}>
      <button type="button" className="source-tree-folder" aria-expanded={open} onClick={() => {
        setCollapsed(previous => {
          const next = new Set(previous);
          if (next.has(node.path)) next.delete(node.path);
          else next.add(node.path);
          return next;
        });
      }}><Chevron size={14} /><Icon size={15} /><span>{node.name}</span></button>
      {open && <ul className="source-tree-children">
        {Array.from(node.folders.values(), renderFolder)}
        {node.files.map(({ name, index }) => <li key={index}><label className="source-tree-file">
          <input type="checkbox" checked={selected[index]} disabled={disabled}
            onChange={event => onCheck(index, event.target.checked)} />
          <FileIcon path={name} /><span>{name}</span>
        </label></li>)}
      </ul>}
    </li>;
  }

  return <ul aria-label="Cây thư mục kết quả" className="source-collection-results">
    {Array.from(roots.values(), renderFolder)}
  </ul>;
}
