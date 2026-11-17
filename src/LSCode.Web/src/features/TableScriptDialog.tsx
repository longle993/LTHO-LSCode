import { Modal, Button } from "../ui";

export function TableScriptDialog({ title, script, onClose, onCopy, onOpen }: {
  title: string;
  script: string;
  onClose: () => void;
  onCopy: () => void;
  onOpen: () => void;
}) {
  return <Modal wide title={title} onClose={onClose}>
    <pre className="table-script-preview" aria-label="Lệnh SQL">{script}</pre>
    <div className="modal-footer table-script-actions">
      <Button onClick={onCopy}>Sao chép</Button>
      <Button onClick={onOpen}>Mở thành tab SQL</Button>
      <Button variant="primary" onClick={onClose}>Đóng</Button>
    </div>
  </Modal>;
}
