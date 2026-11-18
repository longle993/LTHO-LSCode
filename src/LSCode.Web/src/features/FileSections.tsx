import { useEffect, useMemo, useState } from "react";
import { Button, Modal } from "../ui";

type SectionItem = { label: string; detail: string; offset: number };
type SectionGroup = SectionItem & { items: SectionItem[] };

function xmlSections(text: string): SectionGroup[] {
  const tags = /<!--[\s\S]*?(?:-->|$)|<!\[CDATA\[[\s\S]*?(?:\]\]>|$)|<\?[\s\S]*?(?:\?>|$)|<(\/?)([\w:.-]+)(?:[^>"']|"[^"]*"|'[^']*')*>/g;
  const knownGroup = /^(?:fields|script|commands?|css|toolbar|views?|actions|filters|grid|report)$/i;
  const groups: SectionGroup[] = [];
  const stack: { group?: number; label: string; offset: number; end: number }[] = [];
  const fieldMarkers = new Map<number, { first?: SectionItem; last?: SectionItem }>();
  const marker = (label: string, offset: number): SectionItem => ({ label, offset, detail: "" });
  for (const tag of text.matchAll(tags)) {
    if (!tag[2]) continue;
    const label = (tag[2].split(":").at(-1) ?? tag[2]).toLowerCase();
    if (tag[1]) {
      let index = stack.length - 1;
      while (index >= 0 && stack[index].label !== label) index--;
      if (index < 0) continue;
      const entry = stack[index];
      stack.length = index;
      const section = entry.group === undefined ? undefined : groups[entry.group];
      if (section?.offset !== entry.offset) continue;
      if (label === "fields") {
        const fields = fieldMarkers.get(entry.group!);
        section.items = [fields?.first, fields?.last]
          .filter((item): item is SectionItem => item !== undefined);
      }
      if (label === "css") section.items = [marker("End", tag.index), marker("Start", entry.offset)];
      if (label === "script") {
        const body = text.slice(entry.end, tag.index);
        const functions = /\/\*[\s\S]*?\*\/|\/\/[^\r\n]*|"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|`(?:\\.|[^`\\])*`|\bfunction\s*\*?\s+([$\p{L}_][$\p{L}\p{N}_]*)\s*\(/gu;
        for (const fn of body.matchAll(functions)) {
          if (fn[1]) section.items.push(marker(fn[1], entry.end + fn.index));
        }
      }
      continue;
    }
    const detail = /\s(?:name|id|key|controller|event)\s*=\s*["']([^"']+)["']/i.exec(tag[0])?.[1] ?? "";
    const selfClosing = tag[0].endsWith("/>");
    const parentGroup = [...stack].reverse().find((entry) => entry.group !== undefined)?.group;
    const parentLabel = parentGroup === undefined ? undefined : groups[parentGroup]?.label;
    const immediateParent = stack.at(-1)?.label;
    let group = parentGroup;
    const sectionLabel = label === "commands" ? "command" : label;
    if (label === "command" || label === "commands") {
      if (groups[parentGroup ?? -1]?.label !== "command") {
        group = groups.findIndex((item) => item.label === "command");
        if (group < 0) group = groups.push({ label: "command", detail: "", offset: tag.index, items: [] }) - 1;
      }
      if (label === "command" && detail && group !== undefined) groups[group].items.push(marker(detail, tag.index));
    } else if (parentLabel === "fields") {
      if (label === "field" && immediateParent === "fields") {
        const fields = fieldMarkers.get(parentGroup!) ?? {};
        fields.first ??= marker("Start", tag.index);
        fields.last = marker("End", tag.index);
        fieldMarkers.set(parentGroup!, fields);
      }
    } else if (parentLabel === "views") {
      if (label === "view" && immediateParent === "views")
        groups[parentGroup!].items.push(marker(detail || "view", tag.index));
    } else if (parentLabel === "command" || parentLabel === "script") {
      // Nội dung XML/SQL bên trong command và script không phải mục điều hướng.
    } else if (stack.length === 1 || knownGroup.test(label)) {
      group = groups.push({ label: sectionLabel, detail, offset: tag.index, items: [] }) - 1;
    } else if (label !== "items" && label !== "text" && parentGroup !== undefined && (detail || stack.length <= 3)) {
      groups[parentGroup].items.push({ label, detail, offset: tag.index });
    }
    if (!selfClosing) stack.push({ group, label, offset: tag.index, end: tag.index + tag[0].length });
  }
  return groups;
}

export function FileSections({ text, name, onJump, onClose }: {
  text: string;
  name: string;
  onJump: (offset: number) => void;
  onClose: () => void;
}) {
  const [query, setQuery] = useState("");
  const groups = useMemo(() => {
    if (/\.(xml|f|ent|config|xaml|aspx|html?)$/i.test(name)) return xmlSections(text);
    const headings = /^\s*(?:#region\s+(.+)|#{1,6}\s+(.+)|((?:CREATE(?:\s+OR\s+ALTER)?|ALTER)\s+(?:PROCEDURE|PROC|FUNCTION|VIEW|TRIGGER|TABLE)\s+[^\r\n]+))/gim;
    return [...text.matchAll(headings)].map((match) => ({
      label: (match[1] ?? match[2] ?? match[3]).trim(), detail: "", offset: match.index, items: [],
    }));
  }, [name, text]);
  const matches = useMemo(() => {
    const needle = query.trim().toLowerCase();
    if (!needle) return groups;
    return groups.flatMap((group) => {
      const items = group.items.filter((item) => `${item.label} ${item.detail}`.toLowerCase().includes(needle));
      return `${group.label} ${group.detail}`.toLowerCase().includes(needle) || items.length
        ? [{ ...group, items: items.length ? items : group.items }]
        : [];
    });
  }, [groups, query]);
  const [selected, setSelected] = useState(0);
  useEffect(() => setSelected(0), [query, name]);
  const group = matches[selected];
  return (
    <Modal title="Go to" onClose={onClose}>
      <div className="tool-body">
        <input autoFocus aria-label="Tìm phần trong file" placeholder="Tìm theo tên thẻ, name hoặc id" value={query}
          onChange={(event) => setQuery(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "ArrowDown" || event.key === "ArrowUp") {
              event.preventDefault();
              setSelected((index) => Math.max(0, Math.min(matches.length - 1, index + (event.key === "ArrowDown" ? 1 : -1))));
            }
            if (event.key === "Enter" && group) onJump(group.items[0]?.offset ?? group.offset);
          }} />
        <div className="file-section-browser">
          <div className="file-section-heading">TAG</div>
          <div className="file-section-heading">DETAIL</div>
          <div className="file-section-groups" role="listbox" aria-label="Nhóm thẻ XML">
            {matches.map((item, index) => <Button key={item.offset} variant="ghost" role="option"
              className={`file-section-tag file-section-tag-${item.label}`}
              aria-selected={index === selected} onClick={() => setSelected(index)}>
              <span>{item.label}</span>{item.detail && <small>{item.detail}</small>}
            </Button>)}
          </div>
          <div className="file-section-items" data-section={group?.label} role="listbox" aria-label="Mục trong nhóm">
            {group && !group.items.length && <Button variant="ghost" onClick={() => onJump(group.offset)}>
              <span>Start</span><small>{group.label}</small>
            </Button>}
            {group?.items.map((item) => <Button key={item.offset} variant="ghost" onClick={() => onJump(item.offset)}>
              <span>{item.label}</span>{item.detail && <small>{item.detail}</small>}
            </Button>)}
          </div>
        </div>
        {!matches.length && <p className="muted">Không tìm thấy phần phù hợp.</p>}
      </div>
    </Modal>
  );
}
