import { useState } from "react";
import { Button, Modal } from "../ui";

export function CommandBox({ onClose, onOpenRequestTracking }: {
  onClose: () => void;
  onOpenRequestTracking: () => void;
}) {
  const [value, setValue] = useState("");
  const normalized = value.trim();
  const run = () => {
    if (normalized !== "@@") return;
    onOpenRequestTracking();
  };
  return (
    <Modal title="Lệnh" onClose={onClose} className="command-box-modal">
      <form className="command-box" onSubmit={(event) => { event.preventDefault(); run(); }}>
        <label htmlFor="quick-command">Nhập lệnh</label>
        <input id="quick-command" autoFocus value={value} placeholder="Gõ @@ để mở QLYC"
          onChange={(event) => setValue(event.target.value)} />
        <button type="button" className="command-suggestion" aria-selected={normalized === "@@"}
          onClick={onOpenRequestTracking}>
          <code>@@</code>
          <span><strong>QLYC</strong><small>Quản lý và thống kê yêu cầu</small></span>
        </button>
        {normalized && normalized !== "@@" && <p className="muted">Không tìm thấy lệnh phù hợp.</p>}
        <div className="modal-footer">
          <Button type="button" onClick={onClose}>Hủy</Button>
          <Button type="submit" variant="primary" disabled={normalized !== "@@"}>Mở</Button>
        </div>
      </form>
    </Modal>
  );
}
