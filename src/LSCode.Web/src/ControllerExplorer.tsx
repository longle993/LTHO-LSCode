import { useEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Button, Modal } from "./ui";
import { pathIsWithin, rebasePath } from "./fileLabels";
import { ChevronRight, ChevronDown, Folder, FileCode2, FileTerminal, PanelsTopLeft,
  ChartNoAxesCombined, FileSpreadsheet, FileText, FileChartColumn } from "lucide-react";

export function FileIcon({ path }: { path: string }) {
  const ext = path.split(".").at(-1)?.toLowerCase() ?? "";
  const Icon = ext === "xlsx" ? FileSpreadsheet : ext === "frx" ? FileChartColumn :
    ext === "rpt" ? ChartNoAxesCombined : ext === "txt" ? FileText :
    ext === "f" ? FileTerminal : ext === "aspx" ? PanelsTopLeft : FileCode2;
  return <Icon size={15} className={"file-icon file-" + ext} />;
}

type ExplorerItem = { name: string; path: string; directory: boolean };
const pathKey = (path: string) => path.replaceAll("\\", "/").replace(/\/+$/, "").toLowerCase();
const parentFolder = (path: string) => {
  const separator = Math.max(path.lastIndexOf("/"), path.lastIndexOf("\\"));
  return path.slice(0, separator === 0 || (separator === 2 && path[1] === ":") ? separator + 1 : separator);
};
const categories = ["Main", "Dir", "Filter", "Grid", "Report", "Templates"];
export const folderKind = (name: string) =>
  categories.find((category) => category.toLowerCase() === name.toLowerCase())?.toLowerCase() ?? "folder";

export default function ControllerExplorer({
  root, files, activePath, onOpen, onCopy, onReveal, onDuplicate, onRename,
  emptyMessage = "Mở một chức năng ERP để xem file của controller.",
}: {
  root: string;
  files?: string[];
  activePath?: string | null;
  onOpen: (path: string) => void;
  onCopy: (text: string) => void;
  onReveal: (path: string) => void;
  onDuplicate?: (path: string) => void;
  onRename?: (path: string, name: string) => Promise<string>;
  emptyMessage?: string;
}) {
  const [collapsed, setCollapsed] = useState(new Set<string>());
  const directories = useMemo(() => {
    const result = new Map<string, Map<string, ExplorerItem>>();
    for (const file of files ?? []) {
      if (!pathIsWithin(file, root)) continue;
      let path = file;
      let directory = false;
      while (pathKey(path) !== pathKey(root)) {
        const parent = parentFolder(path);
        if (!parent || parent === path) break;
        const key = pathKey(parent);
        const entries = result.get(key) ?? new Map<string, ExplorerItem>();
        entries.set(pathKey(path), { path, name: path.slice(parent.length).replace(/^[\\/]+/, ""), directory });
        result.set(key, entries);
        path = parent;
        directory = true;
      }
    }
    return new Map([...result].map(([path, entries]) => [
      path, [...entries.values()].sort((a, b) => Number(b.directory) - Number(a.directory) || a.name.localeCompare(b.name)),
    ]));
  }, [files, root]);
  const [selected, setSelected] = useState<string>();
  const [menu, setMenu] = useState<{ item: ExplorerItem; x: number; y: number }>();
  const [renaming, setRenaming] = useState<ExplorerItem>();
  const [newName, setNewName] = useState("");
  const [renameBusy, setRenameBusy] = useState(false);
  const [renameError, setRenameError] = useState("");
  const menuElement = useRef<HTMLDivElement>(null);
  const menuOwner = useRef<HTMLButtonElement>();
  const renameInput = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (!menu) return;
    const dismiss = (event: PointerEvent) => {
      if (!menuElement.current?.contains(event.target as Node)) setMenu(undefined);
    };
    document.addEventListener("pointerdown", dismiss);
    menuElement.current?.querySelector("button")?.focus();
    return () => document.removeEventListener("pointerdown", dismiss);
  }, [menu]);
  useEffect(() => {
    if (renaming) renameInput.current?.focus();
  }, [renaming]);
  function beginRename(item: ExplorerItem) {
    setMenu(undefined);
    setRenaming(item);
    setNewName(item.name);
    setRenameError("");
  }
  const actions = (item: ExplorerItem) => ({
    title: item.path,
    "data-selected": selected === item.path || undefined,
    onFocus: () => setSelected(item.path),
    onContextMenu: (event: React.MouseEvent<HTMLButtonElement>) => {
      event.preventDefault();
      event.currentTarget.focus();
      menuOwner.current = event.currentTarget;
      setSelected(item.path);
      setMenu({ item, x: Math.max(8, Math.min(event.clientX, innerWidth - 248)),
        y: Math.max(8, Math.min(event.clientY, innerHeight - 220)) });
    },
    onKeyDown: (event: React.KeyboardEvent<HTMLButtonElement>) => {
      if (event.key === "F2" && onRename) {
        event.preventDefault();
        event.stopPropagation();
        beginRename(item);
      } else if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "c") {
        event.preventDefault();
        event.stopPropagation();
        onCopy(item.name);
      }
    },
  });
  const closeMenu = () => { setMenu(undefined); menuOwner.current?.focus(); };

  function tree(path: string, inheritedKind = "folder"): React.ReactNode {
    return directories.get(pathKey(path))?.map((item) => {
      if (!item.directory) return <button
        key={item.path}
        className="tree-row"
        aria-current={item.path.toLowerCase() === activePath?.toLowerCase() ? "page" : undefined}
        {...actions(item)}
        onClick={() => onOpen(item.path)}
      ><FileIcon path={item.path} /><span>{item.name}</span></button>;
      const category = folderKind(item.name);
      const kind = category === "folder" ? inheritedKind : category;
      const open = !collapsed.has(item.path);
      return <div key={item.path} className={"controller-folder category-" + kind} data-category={kind}>
        <button
          className={"tree-row folder-row " + (category !== "folder" ? "category-heading" : "")}
          aria-expanded={open}
          data-active={!!activePath && pathIsWithin(activePath, item.path) || undefined}
          {...actions(item)}
          onClick={() => {
            setCollapsed((old) => {
              const next = new Set(old);
              open ? next.add(item.path) : next.delete(item.path);
              return next;
            });
          }}
        >
          <span className="folder-chevron">{open ? <ChevronDown size={12} /> : <ChevronRight size={12} />}</span>
          <Folder size={15} className="folder-icon" /><span>{item.name}</span>
        </button>
        {open && <div className="tree-children">{tree(item.path, kind)}</div>}
      </div>;
    });
  }
  return <>
    <div className="project-file-tree">{files?.length ? tree(root) :
      <div className="explorer-message">{emptyMessage}</div>}</div>
    {menu && createPortal(
      <div
        ref={menuElement}
        className="table-action-menu explorer-action-menu"
        role="menu"
        aria-label="Thao tác file và thư mục"
        style={{ left: menu.x, top: menu.y }}
        onKeyDown={(event) => {
          if (event.key === "Escape") { event.preventDefault(); closeMenu(); }
          if (event.key === "Tab") setMenu(undefined);
          if (event.key === "ArrowDown" || event.key === "ArrowUp") {
            event.preventDefault();
            const buttons = Array.from(menuElement.current?.querySelectorAll("button") ?? []);
            const index = buttons.indexOf(document.activeElement as HTMLButtonElement);
            buttons[(index + (event.key === "ArrowDown" ? 1 : -1) + buttons.length) % buttons.length]?.focus();
          }
        }}
      >
        <Button role="menuitem" variant="ghost" onClick={() => { closeMenu(); onCopy(menu.item.name); }}>Sao chép tên <kbd>Ctrl+C</kbd></Button>
        <Button role="menuitem" variant="ghost" onClick={() => { closeMenu(); onCopy(menu.item.path); }}>Sao chép đường dẫn</Button>
        {onRename && <Button role="menuitem" variant="ghost" onClick={() => beginRename(menu.item)}>Đổi tên <kbd>F2</kbd></Button>}
        <Button role="menuitem" variant="ghost" onClick={() => {
          closeMenu();
          onReveal(menu.item.directory ? menu.item.path : parentFolder(menu.item.path));
        }}>{menu.item.directory ? "Mở thư mục" : "Mở thư mục chứa file"}</Button>
        {!menu.item.directory && onDuplicate && <Button role="menuitem" variant="ghost" onClick={() => { closeMenu(); onDuplicate(menu.item.path); }}>Nhân bản file</Button>}
      </div>, document.body)}
    {renaming && onRename && <Modal title={renaming.directory ? "Đổi tên thư mục" : "Đổi tên file"} onClose={() => { if (!renameBusy) setRenaming(undefined); }}>
      <form className="tool-body" onSubmit={(event) => {
        event.preventDefault();
        if (renameBusy) return;
        setRenameBusy(true);
        setRenameError("");
        void onRename(renaming.path, newName).then((destination) => {
          setCollapsed((paths) => new Set([...paths].map((path) => rebasePath(path, renaming.path, destination))));
          setSelected(destination);
          setRenaming(undefined);
        }).catch((error) => setRenameError((error as Error).message))
          .finally(() => setRenameBusy(false));
      }}>
        <label>Tên mới<input ref={renameInput} aria-label="Tên mới" value={newName} disabled={renameBusy}
          onChange={(event) => setNewName(event.target.value)}
          onFocus={(event) => {
            const extension = renaming.directory ? -1 : newName.lastIndexOf(".");
            event.currentTarget.setSelectionRange(0, extension > 0 ? extension : newName.length);
          }} /></label>
        {renameError && <p role="alert" className="form-error">{renameError}</p>}
        <div className="inline-checks">
          <Button type="button" disabled={renameBusy} onClick={() => setRenaming(undefined)}>Hủy</Button>
          <Button type="submit" variant="primary" disabled={renameBusy || !newName.trim()}>{renameBusy ? "Đang đổi tên…" : "Đổi tên"}</Button>
        </div>
      </form>
    </Modal>}
  </>;
}
