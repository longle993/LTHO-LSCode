import { Modal } from "./ui";
import { useEffect, useState } from "react";
import { CatalogTool } from "./catalog";
import type { ToolProps } from "./features/types";
export { ConnectionDialog } from "./features/ConnectionDialog";
export { ProjectDialog } from "./features/ProjectDialog";
export { SettingsDialog } from "./features/SettingsDialog";
import { SnippetTool } from "./features/SnippetTool";
import { CompareTool } from "./features/CompareTool";
import { XmlTool } from "./features/XmlTool";
import { TranslationTool } from "./features/TranslationTool";
import { BeautifyTool } from "./features/BeautifyTool";
export function ToolDialog(props: ToolProps) {
  const [dirty, setDirty] = useState(false),
    [busy, setBusy] = useState(false);
  useEffect(() => {
    props.onDirtyChange?.(dirty);
    return () => props.onDirtyChange?.(false);
  }, [dirty, props.onDirtyChange]);
  const titles: Record<string, string> = {
    catalog: "Thiết kế danh mục",
    snippets: "Thư viện snippets",
    compare: "So sánh",
    xml: "Công cụ XML",
    translation: "Dịch văn bản",
    beautify: "Beautify SQL",
  };
  return (
    <Modal
      title={titles[props.kind] ?? props.kind}
      onClose={() => {
        if (
          !busy &&
          (!dirty || window.confirm("Bỏ bản nháp danh mục chưa lưu?"))
        )
          props.onClose();
      }}
      wide
    >
      {props.kind === "beautify" ? (
        <BeautifyTool {...props} />
      ) : props.kind === "catalog" ? (
        <CatalogTool
          target={props.target}
          onDirtyChange={setDirty}
          onBusyChange={setBusy}
        />
      ) : props.kind === "snippets" ? (
        <SnippetTool {...props} />
      ) : props.kind === "compare" ? (
        <CompareTool {...props} />
      ) : props.kind === "xml" ? (
        <XmlTool {...props} />
      ) : props.kind === "translation" ? (
        <TranslationTool {...props} />
      ) : (
        <div className="empty">Công cụ không xác định.</div>
      )}
    </Modal>
  );
}
