import { useMemo, useState } from "react";
import { Button, Modal } from "../ui";

export function FileSections({ text, name, onJump, onClose }: {
  text: string;
  name: string;
  onJump: (offset: number) => void;
  onClose: () => void;
}) {
  const [query, setQuery] = useState("");
  const sections = useMemo(() => {
    if (/\.(xml|f|ent|config|xaml|aspx|html?)$/i.test(name)) {
      const tags = /<!--[\s\S]*?(?:-->|$)|<!\[CDATA\[[\s\S]*?(?:\]\]>|$)|<\?[\s\S]*?(?:\?>|$)|<(\/?)([\w:.-]+)(?:[^>"']|"[^"]*"|'[^']*')*>/g;
      const known = /^(?:fields|script|commands?|css|toolbar|views?|items|actions|filters|grid|report)$/i;
      let depth = 0;
      return [...text.matchAll(tags)].flatMap((tag) => {
        if (!tag[2]) return [];
        if (tag[1]) {
          depth = Math.max(0, depth - 1);
          return [];
        }
        const label = tag[2].split(":").at(-1) ?? tag[2];
        const show = depth <= 1 || known.test(label);
        if (!tag[0].endsWith("/>")) depth++;
        return show ? [{ label, offset: tag.index }] : [];
      });
    }
    const headings = /^\s*(?:#region\s+(.+)|#{1,6}\s+(.+)|((?:CREATE(?:\s+OR\s+ALTER)?|ALTER)\s+(?:PROCEDURE|PROC|FUNCTION|VIEW|TRIGGER|TABLE)\s+[^\r\n]+))/gim;
    return [...text.matchAll(headings)].map((match) => ({
      label: (match[1] ?? match[2] ?? match[3]).trim(),
      offset: match.index,
    }));
  }, [name, text]);
  const matches = sections.filter((section) => section.label.toLowerCase().includes(query.trim().toLowerCase()));
  return (
    <Modal title="Đi đến phần trong file" onClose={onClose}>
      <div className="tool-body">
        <input autoFocus aria-label="Tìm phần trong file" placeholder="Tìm theo tên phần" value={query}
          onChange={(event) => setQuery(event.target.value)}
          onKeyDown={(event) => { if (event.key === "Enter" && matches[0]) onJump(matches[0].offset); }} />
        <div className="file-section-list">
          {matches.map(({ label, offset }) => (
            <Button key={offset} variant="ghost" onClick={() => onJump(offset)}>{label}</Button>
          ))}
          {!matches.length && <p className="muted">Không tìm thấy phần phù hợp.</p>}
        </div>
      </div>
    </Modal>
  );
}
