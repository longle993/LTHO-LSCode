import { Button, ConfirmDialog, Modal } from "./ui";
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
import { CatalogDescriptionTool } from "./features/CatalogDescriptionTool";
import { CatalogSourceTool } from "./features/CatalogSourceTool";
export function ToolDialog(props: ToolProps) {
  const [dirty, setDirty] = useState(false),
    [busy, setBusy] = useState(false);
  const [confirmation, setConfirmation] = useState<{ message: string; action: () => void }>();
  const [catalogMode, setCatalogMode] = useState(props.workspaceRoot ? "source" : "sql");
  function guardDraft(action: () => void) {
    if (busy) return;
    if (!dirty) action();
    else setConfirmation({ message: "Bản nháp danh mục chưa được lưu. Bạn có muốn bỏ các thay đổi này?", action });
  }
  function switchCatalogMode(mode: string) {
    if (mode === catalogMode || busy) return;
    guardDraft(() => {
      setDirty(false);
      setCatalogMode(mode);
    });
  }
  useEffect(() => {
    props.onDirtyChange?.(dirty);
    return () => props.onDirtyChange?.(false);
  }, [dirty, props.onDirtyChange]);
  const titles: Record<string, string> = {
    catalog: "Tạo danh mục",
    snippets: "Thư viện snippets",
    compare: "So sánh",
    xml: "Công cụ XML",
    translation: "Dịch văn bản",
    beautify: "Beautify SQL",
  };
  const catalog = <div className="catalog-workspace">
    <div className="catalog-mode" aria-label="Chế độ tạo danh mục">
      <Button variant={catalogMode === "description" ? "primary" : "outline"} aria-pressed={catalogMode === "description"}
        disabled={busy} onClick={() => switchCatalogMode("description")}>Tạo từ mô tả</Button>
      <Button variant={catalogMode === "source" ? "primary" : "outline"} aria-pressed={catalogMode === "source"}
        disabled={busy} onClick={() => switchCatalogMode("source")}>Sinh source ERP</Button>
      <Button variant={catalogMode === "sql" ? "primary" : "outline"} aria-pressed={catalogMode === "sql"}
        disabled={busy} onClick={() => switchCatalogMode("sql")}>Thiết kế bảng SQL</Button>
    </div>
    {catalogMode === "description" ? <CatalogDescriptionTool root={props.workspaceRoot ?? ""}
      template={props.catalogTemplate ?? ""} app={props.catalogApp} sys={props.catalogSys}
      onBusy={setBusy} onDirty={setDirty} onOpen={props.onOpen} /> :
      catalogMode === "source" ? <CatalogSourceTool root={props.workspaceRoot ?? ""}
      initialTemplate={props.catalogTemplate ?? ""} onDirty={setDirty} onBusy={setBusy} /> :
      <CatalogTool target={props.target} onDirtyChange={setDirty} onBusyChange={setBusy} />}
  </div>;
  return <>
    <Modal
      title={titles[props.kind] ?? props.kind}
      onClose={() => guardDraft(props.onClose)}
      wide
      className={props.kind === "catalog" ? "catalog-tool-modal" : ""}
    >
      {props.kind === "beautify" ? (
        <BeautifyTool {...props} />
      ) : props.kind === "catalog" ? catalog : props.kind === "snippets" ? (
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
    {confirmation && <ConfirmDialog title="Bỏ bản nháp?" message={confirmation.message} confirmLabel="Bỏ thay đổi" danger
      onClose={() => setConfirmation(undefined)} onConfirm={() => {
        const action = confirmation.action;
        setConfirmation(undefined);
        action();
      }} />}
  </>;
}
