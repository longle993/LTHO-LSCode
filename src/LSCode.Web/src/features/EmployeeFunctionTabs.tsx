import { ChevronDown, ChevronUp } from "lucide-react";
import { useEffect, useId, useState } from "react";
import { operation } from "../bridge";

type FunctionTab = { sysId: string; caption: string };

export function EmployeeFunctionTabs({ targetId, selected, onOpen }: {
  targetId?: string;
  selected: string;
  onOpen: (sysId: string) => void;
}) {
  const id = useId();
  const [expanded, setExpanded] = useState(false);
  const [tabs, setTabs] = useState<FunctionTab[]>();
  const [error, setError] = useState("");

  useEffect(() => {
    if (!expanded || tabs || !targetId) return;
    let active = true;
    const load = operation<FunctionTab[]>("project.employeeTabs", { targetId }, 20_000);
    load.promise.then((rows) => {
      if (active) setTabs(rows.filter((row) => /^[\w$-]+$/.test(row.sysId)));
    }).catch((reason) => {
      if (active) setError(reason.message);
    });
    return () => { active = false; };
  }, [expanded, tabs, targetId]);

  return <div className="employee-function-tabs">
    <button className="tree-row employee-function-toggle" aria-expanded={expanded} aria-controls={id}
      onClick={() => { setError(""); setExpanded(!expanded); }}>
      {expanded ? <ChevronDown size={14} /> : <ChevronUp size={14} />}
      <span>Tab chức năng</span>
    </button>
    <div id={id} className="employee-function-list" hidden={!expanded}
      aria-busy={expanded && !!targetId && !tabs && !error}>
      {!targetId ? <div className="explorer-message">Dự án chưa có kết nối SYS.</div>
        : error ? <div className="explorer-message" role="alert">{error}</div>
        : !tabs ? <div className="explorer-message" role="status">Đang tải tab chức năng…</div>
        : !tabs.length ? <div className="explorer-message">Chưa có tab chức năng.</div>
        : tabs.map(({ sysId, caption }, index) => (
          <button key={index} className="tree-row employee-function-entry" title={caption + " · " + sysId}
            aria-current={sysId.toLowerCase() === selected ? "true" : undefined} onClick={() => onOpen(sysId)}>
            <span>{caption}</span>
          </button>
        ))}
    </div>
  </div>;
}
