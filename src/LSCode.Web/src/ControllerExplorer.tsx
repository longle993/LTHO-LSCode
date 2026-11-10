import { useState } from "react";
import {
  ChevronRight,
  ChevronDown,
  Folder,
  Workflow,
  FileCode2,
  FileTerminal,
  PanelsTopLeft,
  ChartNoAxesCombined,
  FileSpreadsheet,
  FileText,
  FileChartColumn,
  X,
} from "lucide-react";
export function FileIcon({ path }: { path: string }) {
  const ext = path.split(".").at(-1)?.toLowerCase() ?? "";
  const Icon =
    ext === "xlsx"
      ? FileSpreadsheet
      : ext === "frx"
        ? FileChartColumn
        : ext === "rpt"
          ? ChartNoAxesCombined
          : ext === "txt"
            ? FileText
            : ext === "f"
              ? FileTerminal
              : ext === "aspx"
                ? PanelsTopLeft
                : FileCode2;
  return <Icon size={15} className={"file-icon file-" + ext} />;
}
type Node = { name: string; path?: string; children: Map<string, Node> };
const categories = ["Main", "Dir", "Filter", "Grid", "Report", "Templates"];
export const folderKind = (name: string) =>
  categories
    .find((c) => c.toLowerCase() === name.toLowerCase())
    ?.toLowerCase() ?? "folder";
// Hide physical infrastructure segments without changing the actual file paths used to open/save.
export function controllerTree(files: string[]): Node[] {
  const root = new Map<string, Node>();
  const all = files.map((path) => ({
    path,
    parts: path.replaceAll("\\", "/").split("/").filter(Boolean),
  }));
  let common = 0;
  while (
    all.length &&
    common < all[0].parts.length - 1 &&
    all.every(
      (f) =>
        f.parts[common]?.toLowerCase() === all[0].parts[common].toLowerCase(),
    )
  )
    common++;
  for (const { path, parts } of all) {
    const controller = parts.findIndex(
      (p) => p.toLowerCase() === "controllers",
    );
    const appData = parts.findIndex((p) => /^app_?data$/i.test(p));
    const main = parts.findIndex((p) => p.toLowerCase() === "main");
    let relative =
      main >= 0
        ? parts.slice(main)
        : controller >= 0
          ? parts.slice(controller + 1)
          : appData >= 0
            ? parts.slice(appData + 1)
            : parts.slice(common);
    relative = relative.filter(
      (p, i) =>
        i === relative.length - 1 || !/^app_?data$|^controllers$/i.test(p),
    );
    if (relative.length === 1 && /\.aspx$/i.test(relative[0]))
      relative = ["Main", ...relative];
    let children = root;
    relative.forEach((name, i) => {
      const key = name.toLowerCase();
      let node = children.get(key);
      if (!node) {
        node = { name, children: new Map() };
        children.set(key, node);
      }
      if (i === relative.length - 1) node.path = path;
      children = node.children;
    });
  }
  return [...root.values()];
}
export default function ControllerExplorer({
  groups,
  expandedGroup,
  onExpandGroup,
  activePath,
  onOpen,
  onClose,
}: {
  groups: { name: string; files: string[] }[];
  expandedGroup?: string;
  onExpandGroup: (name?: string) => void;
  activePath?: string | null;
  onOpen: (p: string) => void;
  onClose: (p: string) => void;
}) {
  const [closed, setClosed] = useState(new Set<string>());
  const toggle = (key: string) =>
    setClosed((old) => {
      const next = new Set(old);
      next.has(key) ? next.delete(key) : next.add(key);
      return next;
    });
  const contains = (node: Node): boolean =>
    node.path === activePath || [...node.children.values()].some(contains);
  function tree(nodes: Node[], prefix: string, level = 0): React.ReactNode {
    return nodes
      .sort((a, b) => {
        if (!!a.path !== !!b.path) return a.path ? 1 : -1;
        const ai = categories.findIndex(
            (c) => c.toLowerCase() === a.name.toLowerCase(),
          ),
          bi = categories.findIndex(
            (c) => c.toLowerCase() === b.name.toLowerCase(),
          );
        return (
          (ai < 0 ? 99 : ai) - (bi < 0 ? 99 : bi) ||
          a.name.localeCompare(b.name)
        );
      })
      .map((node) => {
        const key = prefix + "/" + node.name;
        if (node.path)
          return (
            <button
              key={key}
              className="tree-row"
              title={node.path}
              aria-current={node.path === activePath ? "page" : undefined}
              onClick={() => onOpen(node.path!)}
            >
              <FileIcon path={node.path} />
              <span>{node.name}</span>
            </button>
          );
        const kind = folderKind(node.name);
        return (
          <div
            key={key}
            className={"controller-folder category-" + kind}
            data-category={kind}
          >
            <button
              className={
                "tree-row folder-row " + (level === 0 ? "category-heading" : "")
              }
              aria-expanded={!closed.has(key)}
              data-active={contains(node) || undefined}
              onClick={() => toggle(key)}
            >
              <span className="folder-chevron">
                {closed.has(key) ? (
                  <ChevronRight size={12} />
                ) : (
                  <ChevronDown size={12} />
                )}
              </span>
              <Folder size={15} className="folder-icon" />
              <span>{node.name}</span>
            </button>
            {!closed.has(key) && (
              <div className="tree-children">
                {tree([...node.children.values()], key, level + 1)}
              </div>
            )}
          </div>
        );
      });
  }
  return (
    <>
      {groups.length === 0 && (
        <div className="empty">
          Mở chức năng để thêm controller. Các controller đã mở sẽ xuất hiện tại
          đây.
        </div>
      )}
      {groups.map((g) => (
        <section key={g.name} className="controller-group">
          <div className="controller-heading">
            <button
              className="tree-row"
              title={g.name}
              onClick={() => onExpandGroup(expandedGroup === g.name ? undefined : g.name)}
              aria-expanded={expandedGroup === g.name}
            >
              {expandedGroup !== g.name ? (
                <ChevronRight size={13} />
              ) : (
                <ChevronDown size={13} />
              )}
              <Workflow size={16} className="controller-icon" />
              <strong>
                {g.name
                  .split(/[\\/]/)
                  .at(-1)
                  ?.replace(/\.aspx$/i, "")}
              </strong>
            </button>
            <button
              className="button ghost icon-button"
              aria-label={"Đóng controller " + g.name}
              onClick={() => {
                if (expandedGroup === g.name) onExpandGroup(undefined);
                onClose(g.name);
              }}
            >
              <X size={13} />
            </button>
          </div>
          {expandedGroup === g.name && tree(controllerTree(g.files), g.name)}
        </section>
      ))}
    </>
  );
}
